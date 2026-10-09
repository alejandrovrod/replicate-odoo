import { useEffect, useMemo, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { useTranslation } from 'react-i18next'
import { useBankAccounts } from '../api/useBankAccounts'
import { useCustomers } from '../../selling/api/useCustomers'
import { useSuppliers } from '../../buying/api/useSuppliers'
import { getOutstandingInvoices, type OutstandingInvoice } from '../api/usePaymentEntries'

interface AllocationDraft {
  invoiceId: string
  isSales: boolean
  allocatedAmount: number
}

interface PaymentEntryModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: {
    paymentType: string
    partyType: string
    partyId: string
    bankAccountId: string
    paymentDate: string
    paidAmount: number
    referenceNumber?: string | null
    allocations: { salesInvoiceId?: string | null; purchaseInvoiceId?: string | null; allocatedAmount: number }[]
    partyName?: string
    modeOfPayment?: string
    remarks?: string
  }) => Promise<void>
  companyId: string
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-500 pt-2">{children}</h4>
}

export function PaymentEntryModal({ isOpen, onClose, onSave, companyId }: PaymentEntryModalProps) {
  const { t } = useTranslation('banking')
  const [paymentType, setPaymentType] = useState('Receive')
  const [partyId, setPartyId] = useState('')
  const [bankAccountId, setBankAccountId] = useState('')
  const [paymentDate, setPaymentDate] = useState(todayIso())
  const [paidAmount, setPaidAmount] = useState(0)
  const [referenceNumber, setReferenceNumber] = useState('')
  const [modeOfPayment, setModeOfPayment] = useState('')
  const [remarks, setRemarks] = useState('')
  const [allocations, setAllocations] = useState<AllocationDraft[]>([])
  const [outstanding, setOutstanding] = useState<OutstandingInvoice[]>([])
  const [loadingInvoices, setLoadingInvoices] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const bankAccountsQuery = useBankAccounts(companyId, 1, 100)
  const customersQuery = useCustomers(companyId, 1, 100)
  const suppliersQuery = useSuppliers(1, 100)
  const isReceive = paymentType === 'Receive'

  useEffect(() => {
    if (isOpen) {
      setPaymentType('Receive')
      setPartyId('')
      setBankAccountId('')
      setPaymentDate(todayIso())
      setPaidAmount(0)
      setReferenceNumber('')
      setModeOfPayment('')
      setRemarks('')
      setAllocations([])
      setOutstanding([])
      setError(null)
    }
  }, [isOpen])

  useEffect(() => {
    setPartyId('')
    setAllocations([])
    setOutstanding([])
  }, [paymentType])

  useEffect(() => {
    if (!isOpen || !partyId) {
      setOutstanding([])
      return
    }
    let cancelled = false
    setLoadingInvoices(true)
    getOutstandingInvoices(isReceive ? 'customers' : 'suppliers', partyId, companyId).then(
      (rows) => {
        if (!cancelled) {
          setOutstanding(rows)
          setLoadingInvoices(false)
        }
      },
      () => {
        if (!cancelled) {
          setOutstanding([])
          setLoadingInvoices(false)
        }
      },
    )
    return () => {
      cancelled = true
    }
  }, [isOpen, partyId, isReceive, companyId])

  const allocatedTotal = useMemo(
    () => allocations.reduce((sum, a) => sum + (Number(a.allocatedAmount) || 0), 0),
    [allocations],
  )
  const unallocated = (Number(paidAmount) || 0) - allocatedTotal

  const toggleAllocation = (invoice: OutstandingInvoice) => {
    setAllocations((prev) => {
      const existing = prev.find((a) => a.invoiceId === invoice.id)
      if (existing) return prev.filter((a) => a.invoiceId !== invoice.id)
      const remaining = (Number(paidAmount) || 0) - prev.reduce((s, a) => s + (Number(a.allocatedAmount) || 0), 0)
      const amount = Math.max(0, Math.min(invoice.outstandingAmount, remaining))
      return [...prev, { invoiceId: invoice.id, isSales: isReceive, allocatedAmount: amount }]
    })
  }

  const updateAllocation = (invoiceId: string, amount: number) => {
    setAllocations((prev) => prev.map((a) => (a.invoiceId === invoiceId ? { ...a, allocatedAmount: amount } : a)))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!partyId || !bankAccountId) {
      setError(t('paymentForm.errorRequired', 'Party and bank account are required.'))
      return
    }
    if (!(Number(paidAmount) > 0)) {
      setError(t('paymentForm.errorAmount', 'Paid amount must be greater than zero.'))
      return
    }
    if (unallocated < 0) {
      setError(t('paymentForm.errorOverAllocated', 'Allocated amount exceeds the paid amount.'))
      return
    }
    setIsSubmitting(true)
    try {
      await onSave({
        paymentType,
        partyType: isReceive ? 'Customer' : 'Supplier',
        partyId,
        bankAccountId,
        paymentDate,
        paidAmount: Number(paidAmount),
        referenceNumber: referenceNumber || null,
        allocations: allocations.map((a) => ({
          salesInvoiceId: a.isSales ? a.invoiceId : null,
          purchaseInvoiceId: a.isSales ? null : a.invoiceId,
          allocatedAmount: Number(a.allocatedAmount),
        })),
        modeOfPayment: modeOfPayment || '',
        remarks: remarks || '',
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('paymentForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  const selectClassName =
    'flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50'

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('paymentForm.titleNew', 'New Payment Entry')}</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <SectionTitle>{t('paymentForm.sectionGeneral', 'Voucher')}</SectionTitle>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.typeLabel', 'Payment Type')}</label>
                <select value={paymentType} onChange={(e) => setPaymentType(e.target.value)} className={selectClassName}>
                  <option value="Receive">{t('paymentForm.typeReceive', 'Receive')}</option>
                  <option value="Pay">{t('paymentForm.typePay', 'Pay')}</option>
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.partyLabel', isReceive ? 'Customer *' : 'Supplier *')}</label>
                <select value={partyId} onChange={(e) => setPartyId(e.target.value)} className={selectClassName} required>
                  <option value="">-- Select --</option>
                  {isReceive
                    ? customersQuery.items.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.code} — {c.name}
                        </option>
                      ))
                    : suppliersQuery.items.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.code} — {s.name}
                        </option>
                      ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.bankLabel', 'Bank Account *')}</label>
                <select value={bankAccountId} onChange={(e) => setBankAccountId(e.target.value)} className={selectClassName} required>
                  <option value="">-- Select --</option>
                  {bankAccountsQuery.items.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.accountName} — {b.accountNumber}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.dateLabel', 'Payment Date *')}</label>
                <Input type="date" value={paymentDate} onChange={(e) => setPaymentDate(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.amountLabel', 'Paid Amount *')}</label>
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={paidAmount || ''}
                  onChange={(e) => setPaidAmount(Number(e.target.value))}
                  placeholder="0.00"
                  required
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.modeLabel', 'Mode of Payment')}</label>
                <Input
                  value={modeOfPayment}
                  onChange={(e) => setModeOfPayment(e.target.value)}
                  placeholder={t('paymentForm.modePlaceholder', 'Cash, Wire Transfer…')}
                />
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('paymentForm.referenceLabel', 'Reference No')}</label>
                <Input
                  value={referenceNumber}
                  onChange={(e) => setReferenceNumber(e.target.value)}
                  placeholder={t('paymentForm.referencePlaceholder', 'Cheque / transfer number')}
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <label className="text-sm font-medium">{t('paymentForm.remarksLabel', 'Remarks')}</label>
                <Input value={remarks} onChange={(e) => setRemarks(e.target.value)} placeholder="—" />
              </div>
            </div>

            <SectionTitle>
              {t('paymentForm.sectionAllocations', 'Allocations')} ·{' '}
              <span className="font-mono normal-case">
                {t('paymentForm.allocatedOf', 'allocated {{a}} of {{p}} (unallocated {{u}})', {
                  a: allocatedTotal.toLocaleString(),
                  p: (Number(paidAmount) || 0).toLocaleString(),
                  u: unallocated.toLocaleString(),
                })}
              </span>
            </SectionTitle>
            {!partyId ? (
              <p className="text-xs text-slate-500">{t('paymentForm.pickPartyFirst', 'Pick a party to list open invoices.')}</p>
            ) : loadingInvoices ? (
              <p className="text-xs text-slate-500">{t('paymentForm.loadingInvoices', 'Loading open invoices…')}</p>
            ) : outstanding.length === 0 ? (
              <p className="text-xs text-slate-500">{t('paymentForm.noOpenInvoices', 'No open invoices — the full amount stays as advance.')}</p>
            ) : (
              <div className="space-y-2">
                {outstanding.map((inv) => {
                  const selected = allocations.find((a) => a.invoiceId === inv.id)
                  return (
                    <div key={inv.id} className="grid grid-cols-12 gap-2 items-center rounded-lg border border-slate-100 px-3 py-2">
                      <label className="col-span-7 flex items-center gap-2 text-sm">
                        <input
                          type="checkbox"
                          checked={Boolean(selected)}
                          onChange={() => toggleAllocation(inv)}
                          className="h-4 w-4 rounded border-slate-300"
                        />
                        <span className="font-mono font-medium">{inv.number}</span>
                        <span className="font-mono text-xs text-slate-500">
                          {t('paymentForm.outstanding', 'outstanding {{o}}', { o: inv.outstandingAmount.toLocaleString() })}
                        </span>
                      </label>
                      <Input
                        type="number"
                        min={0}
                        max={inv.outstandingAmount}
                        step="0.01"
                        disabled={!selected}
                        value={selected ? selected.allocatedAmount : ''}
                        onChange={(e) => updateAllocation(inv.id, Number(e.target.value))}
                        placeholder="0.00"
                        className="col-span-5"
                      />
                    </div>
                  )
                })}
              </div>
            )}
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('paymentForm.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('paymentForm.saving', 'Saving…') : t('paymentForm.save', 'Save Draft')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
