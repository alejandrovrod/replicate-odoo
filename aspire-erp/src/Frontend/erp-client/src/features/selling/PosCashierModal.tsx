import { useState } from 'react'
import { X, Plus, Minus, CreditCard, Banknote, ShoppingBag, ShoppingCart } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { formatMoney } from '../../lib/format'
import { useTenantStore } from '../../store/useTenantStore'

interface PosCashierModalProps {
  onClose: () => void
}

interface CartItem {
  itemId: string
  name: string
  rate: number
  quantity: number
}

// Dummy products for the POS UI
const DUMMY_PRODUCTS = [
  { id: '11111111-1111-1111-1111-111111111111', name: 'Artisan Coffee Beans', rate: 18.50 },
  { id: '22222222-2222-2222-2222-222222222222', name: 'Ceramic Mug', rate: 12.00 },
  { id: '33333333-3333-3333-3333-333333333333', name: 'Pour Over Kit', rate: 45.00 },
  { id: '44444444-4444-4444-4444-444444444444', name: 'Paper Filters (100x)', rate: 6.50 },
]

export function PosCashierModal({ onClose }: PosCashierModalProps) {
  const { t, i18n } = useTranslation('selling')
  const companyId = useTenantStore((state) => state.companyId)
  const [cart, setCart] = useState<CartItem[]>([])

  const addToCart = (product: typeof DUMMY_PRODUCTS[0]) => {
    setCart((prev) => {
      const existing = prev.find((i) => i.itemId === product.id)
      if (existing) {
        return prev.map((i) =>
          i.itemId === product.id ? { ...i, quantity: i.quantity + 1 } : i
        )
      }
      return [...prev, { itemId: product.id, name: product.name, rate: product.rate, quantity: 1 }]
    })
  }

  const updateQuantity = (itemId: string, delta: number) => {
    setCart((prev) =>
      prev.map((i) => {
        if (i.itemId === itemId) {
          const newQty = Math.max(0, i.quantity + delta)
          return { ...i, quantity: newQty }
        }
        return i
      }).filter(i => i.quantity > 0)
    )
  }

  const subtotal = cart.reduce((sum, item) => sum + item.rate * item.quantity, 0)
  const tax = subtotal * 0.10 // 10% dummy tax
  const grandTotal = subtotal + tax

  const { state: submitState, dispatch: submitInvoice, isPending } = useErpAction(
    async (modeOfPayment: 'Cash' | 'Card') => {
      const payload = {
        companyId,
        customerId: '99999999-9999-9999-9999-999999999999', // Dummy Customer ID
        posProfileId: '88888888-8888-8888-8888-888888888888', // Dummy POS Profile
        postingDate: new Date().toISOString().split('T')[0],
        items: cart.map(i => ({
          itemId: i.itemId,
          quantity: i.quantity,
          rate: i.rate
        })),
        payments: [
          {
            modeOfPayment,
            amount: grandTotal
          }
        ]
      }
      const response = await apiClient.post('/v1/sales-invoices/pos', payload)
      return response.data
    }
  )

  if (submitState.isSuccess) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
        <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl">
          <div className="text-center">
            <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100">
              <ShoppingBag className="h-8 w-8 text-emerald-600" />
            </div>
            <h2 className="mt-4 text-xl font-bold text-slate-900">{t('pos.successTitle')}</h2>
            <p className="mt-2 text-sm text-slate-600">
              {t('pos.created', { number: submitState.data?.invoiceNumber })}
            </p>
            <button
              onClick={onClose}
              className="mt-6 w-full rounded-lg bg-slate-900 px-4 py-2.5 font-semibold text-white hover:bg-slate-800"
            >
              {t('pos.done')}
            </button>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
      <div className="flex h-[90vh] w-[90vw] max-w-5xl overflow-hidden rounded-2xl bg-white shadow-2xl">
        {/* Products Grid */}
        <div className="flex-1 overflow-y-auto bg-slate-50 p-6">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-xl font-bold text-slate-900">{t('pos.title')}</h2>
            <button onClick={onClose} aria-label={t('pos.close')} className="rounded-full p-2 text-slate-400 hover:bg-slate-200 hover:text-slate-600">
              <X className="h-5 w-5" />
            </button>
          </div>
          
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            {DUMMY_PRODUCTS.map((product) => (
              <button
                key={product.id}
                onClick={() => addToCart(product)}
                className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white p-6 shadow-sm transition-all hover:border-emerald-300 hover:bg-emerald-50 hover:shadow-md active:scale-95"
              >
                <div className="h-16 w-16 rounded-full bg-slate-100 mb-4 flex items-center justify-center">
                  <ShoppingBag className="h-8 w-8 text-slate-400" />
                </div>
                <span className="text-sm font-semibold text-slate-900 text-center">{product.name}</span>
                <span className="mt-1 text-sm font-mono text-emerald-600">{formatMoney(product.rate)}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Cart Sidebar */}
        <div className="flex w-96 flex-col border-l border-slate-200 bg-white">
          <div className="border-b border-slate-200 p-4">
            <h2 className="text-lg font-bold text-slate-900">{t('cart.title')}</h2>
          </div>
          
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {cart.length === 0 ? (
              <div className="flex h-full flex-col items-center justify-center text-slate-400">
                <ShoppingCart className="h-12 w-12 mb-4 opacity-50" />
                <p>{t('cart.empty')}</p>
              </div>
            ) : (
              cart.map((item) => (
                <div key={item.itemId} className="flex items-center justify-between">
                  <div className="flex-1">
                    <p className="text-sm font-semibold text-slate-900">{item.name}</p>
                    <p className="text-xs font-mono text-slate-500">{formatMoney(item.rate)} {t('cart.perUnit')}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <div className="flex items-center rounded-lg border border-slate-200 bg-slate-50">
                      <button onClick={() => updateQuantity(item.itemId, -1)} className="p-1 text-slate-500 hover:text-slate-900">
                        <Minus className="h-4 w-4" />
                      </button>
                      <span className="w-8 text-center text-sm font-semibold">{item.quantity}</span>
                      <button onClick={() => updateQuantity(item.itemId, 1)} className="p-1 text-slate-500 hover:text-slate-900">
                        <Plus className="h-4 w-4" />
                      </button>
                    </div>
                    <span className="w-16 text-right font-mono font-semibold text-slate-900">
                      {formatMoney(item.rate * item.quantity)}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>

          <div className="border-t border-slate-200 bg-slate-50 p-6">
            <div className="space-y-2 mb-6 text-sm">
              <div className="flex justify-between text-slate-600">
                <span>{t('totals.subtotal')}</span>
                <span className="font-mono">{formatMoney(subtotal)}</span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span>{t('totals.tax')}</span>
                <span className="font-mono">{formatMoney(tax)}</span>
              </div>
              <div className="flex justify-between border-t border-slate-200 pt-2 text-lg font-bold text-slate-900">
                <span>{t('totals.total')}</span>
                <span className="font-mono">{formatMoney(grandTotal)}</span>
              </div>
            </div>

            {submitState.error && (
              <div className="mb-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-600 border border-rose-200">
                {translateErrorCode(i18n, submitState.errorCode, submitState.error)}
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <button
                disabled={cart.length === 0 || isPending}
                onClick={() => submitInvoice('Cash')}
                className="flex items-center justify-center gap-2 rounded-xl bg-emerald-600 p-4 font-semibold text-white transition-colors hover:bg-emerald-700 disabled:opacity-50"
              >
                <Banknote className="h-5 w-5" />
                {t('pay.cash')}
              </button>
              <button
                disabled={cart.length === 0 || isPending}
                onClick={() => submitInvoice('Card')}
                className="flex items-center justify-center gap-2 rounded-xl bg-blue-600 p-4 font-semibold text-white transition-colors hover:bg-blue-700 disabled:opacity-50"
              >
                <CreditCard className="h-5 w-5" />
                {t('pay.card')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
