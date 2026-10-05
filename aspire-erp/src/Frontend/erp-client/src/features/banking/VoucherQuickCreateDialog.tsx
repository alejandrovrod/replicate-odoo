import { useState } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { ReceiptText, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { formatQty } from '../../lib/format'
import { useTenantStore } from '../../store/useTenantStore'
import type { BankTransaction, JournalEntry } from './types'
import { netAmount } from './types'

interface QuickVoucherPayload {
  expenseAccountCode: string
  amount: number
  memo: string
}

interface VoucherQuickCreateDialogProps {
  transaction: BankTransaction
  onClose: () => void
  onCreated: () => void
}

/**
 * On-the-fly voucher dialog (task 6.6, scenario BN-04): posts a balanced SUBMITTED journal
 * voucher from one staging line via `POST .../quick-voucher` and reconciles it atomically.
 * The amount is prefilled with |Deposit - Withdrawal|; the server rejects any other value.
 */
export function VoucherQuickCreateDialog({
  transaction,
  onClose,
  onCreated,
}: VoucherQuickCreateDialogProps) {
  const companyId = useTenantStore((state) => state.companyId)
  const { t, i18n } = useTranslation('banking')
  const expected = netAmount(transaction)
  const [expenseAccountCode, setExpenseAccountCode] = useState('')
  const [amount, setAmount] = useState(expected.toFixed(2))
  const [memo, setMemo] = useState('')

  const { state: voucherState, dispatch: createVoucher, isPending } = useErpAction<
    JournalEntry,
    QuickVoucherPayload
  >(async (payload) => {
    const response = await apiClient.post<JournalEntry>(
      `/v1/bank-transactions/${transaction.id}/quick-voucher`,
      {
        companyId,
        expenseAccountCode: payload.expenseAccountCode,
        amount: payload.amount,
        memo: payload.memo === '' ? null : payload.memo,
        rowVersion: transaction.rowVersion,
      },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    )
    return response.data
  })

  const canSubmit =
    expenseAccountCode.trim() !== '' && Number(amount) > 0 && !isPending

  return (
    <Dialog.Root open onOpenChange={(open) => { if (!open) onClose() }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-sm" />
        <Dialog.Content className="fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl bg-white p-6 shadow-2xl">
          <div className="flex items-start justify-between">
            <div>
              <Dialog.Title className="text-base font-bold text-slate-900">
                {t('voucher.title')}
              </Dialog.Title>
              <Dialog.Description className="mt-1 font-mono text-xs text-slate-500">
                {transaction.description} · {formatQty(expected)} {transaction.currency}
              </Dialog.Description>
            </div>
            <Dialog.Close
              aria-label={t('voucher.close')}
              className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
            >
              <X className="h-5 w-5" />
            </Dialog.Close>
          </div>

          {voucherState.isSuccess && voucherState.data ? (
            <div className="mt-4 text-center">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100">
                <ReceiptText className="h-7 w-7 text-emerald-600" />
              </div>
              <h3 className="mt-3 text-sm font-bold text-slate-900">
                {t('voucher.posted', { no: voucherState.data.voucherNo })}
              </h3>
              <p className="mt-1 font-mono text-xs text-slate-500">
                {t('voucher.postedDetail', {
                  dr: voucherState.data.lines.find((l) => l.debit > 0)?.accountCode,
                  cr: voucherState.data.lines.find((l) => l.credit > 0)?.accountCode,
                })}
              </p>
              <button
                type="button"
                onClick={() => {
                  onCreated()
                  onClose()
                }}
                className="mt-5 w-full rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white hover:bg-slate-800"
              >
                {t('voucher.done')}
              </button>
            </div>
          ) : (
            <div className="mt-4 space-y-3">
              <label className="block text-xs font-semibold text-slate-700">
                {t('voucher.expenseAccount')}
                <input
                  value={expenseAccountCode}
                  onChange={(e) => setExpenseAccountCode(e.target.value)}
                  placeholder={t('voucher.expensePlaceholder')}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs font-normal"
                />
              </label>
              <label className="block text-xs font-semibold text-slate-700">
                {t('voucher.amount', { amount: formatQty(expected) })}
                <input
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  inputMode="decimal"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs font-normal"
                />
              </label>
              <label className="block text-xs font-semibold text-slate-700">
                {t('voucher.memo')}
                <input
                  value={memo}
                  onChange={(e) => setMemo(e.target.value)}
                  placeholder={t('voucher.memoPlaceholder')}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-xs font-normal"
                />
              </label>

              {voucherState.error && (
                <div className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
                  {translateErrorCode(i18n, voucherState.errorCode, voucherState.error)}
                </div>
              )}

              <button
                type="button"
                disabled={!canSubmit}
                onClick={() =>
                  createVoucher({
                    expenseAccountCode: expenseAccountCode.trim(),
                    amount: Number(amount),
                    memo: memo.trim(),
                  })
                }
                className="w-full rounded-lg bg-sky-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
              >
                {isPending ? t('voucher.posting') : t('voucher.post')}
              </button>
            </div>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
