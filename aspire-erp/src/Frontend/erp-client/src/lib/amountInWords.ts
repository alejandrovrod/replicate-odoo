/**
 * Renders a money amount as words for the pay-stub net line ("Neto a cobrar en números y
 * texto"). Currency is USD in this deployment, matching `formatMoney`. Two locales are
 * supported (es/en); anything else falls back to English. Pure and dependency-free so the
 * receipt can call it during render and the outcome is unit-testable.
 */

const EN_ONES = [
  'zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine',
  'ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen', 'sixteen', 'seventeen',
  'eighteen', 'nineteen',
]
const EN_TENS = ['', '', 'twenty', 'thirty', 'forty', 'fifty', 'sixty', 'seventy', 'eighty', 'ninety']

const ES_ONES = [
  'cero', 'uno', 'dos', 'tres', 'cuatro', 'cinco', 'seis', 'siete', 'ocho', 'nueve',
  'diez', 'once', 'doce', 'trece', 'catorce', 'quince', 'dieciséis', 'diecisiete',
  'dieciocho', 'diecinueve',
]
const ES_TWENTIES = [
  'veinte', 'veintiuno', 'veintidós', 'veintitrés', 'veinticuatro', 'veinticinco',
  'veintiséis', 'veintisiete', 'veintiocho', 'veintinueve',
]
const ES_TENS = ['', '', '', 'treinta', 'cuarenta', 'cincuenta', 'sesenta', 'setenta', 'ochenta', 'noventa']
const ES_HUNDREDS = [
  '', 'ciento', 'doscientos', 'trescientos', 'cuatrocientos', 'quinientos',
  'seiscientos', 'setecientos', 'ochocientos', 'novecientos',
]

const GROUP_UNITS = [
  [1_000_000_000, 'billion'],
  [1_000_000, 'million'],
  [1_000, 'thousand'],
] as const

function enUnder1000(value: number): string {
  const parts: string[] = []
  const hundreds = Math.floor(value / 100)
  const rest = value % 100

  if (hundreds > 0) parts.push(`${EN_ONES[hundreds]} hundred`)
  if (rest > 0) {
    if (rest < 20) {
      parts.push(EN_ONES[rest])
    } else {
      const tens = Math.floor(rest / 10)
      const unit = rest % 10
      parts.push(unit === 0 ? EN_TENS[tens] : `${EN_TENS[tens]}-${EN_ONES[unit]}`)
    }
  }

  return parts.join(' ')
}

function enInteger(value: number): string {
  if (value === 0) return 'zero'

  const parts: string[] = []
  let remaining = value
  for (const [size, label] of GROUP_UNITS) {
    const count = Math.floor(remaining / size)
    if (count > 0) {
      parts.push(`${enUnder1000(count)} ${label}`)
      remaining %= size
    }
  }
  if (remaining > 0) parts.push(enUnder1000(remaining))

  return parts.join(' ')
}

/** Apocope of a trailing "uno"/"veintiuno" before a masculine noun or "mil"/"millón". */
function esApocope(words: string): string {
  if (words.endsWith('veintiuno')) return `${words.slice(0, -'veintiuno'.length)}veintiún`
  if (words === 'uno') return 'un'
  if (words.endsWith(' uno')) return `${words.slice(0, -'uno'.length)}un`
  return words
}

function esUnder100(value: number): string {
  if (value < 20) return ES_ONES[value]
  if (value < 30) return ES_TWENTIES[value - 20]
  const tens = Math.floor(value / 10)
  const unit = value % 10
  return unit === 0 ? ES_TENS[tens] : `${ES_TENS[tens]} y ${ES_ONES[unit]}`
}

function esUnder1000(value: number): string {
  if (value < 100) return esUnder100(value)
  if (value === 100) return 'cien'
  const hundreds = Math.floor(value / 100)
  const rest = value % 100
  return rest === 0 ? ES_HUNDREDS[hundreds] : `${ES_HUNDREDS[hundreds]} ${esUnder100(rest)}`
}

function esInteger(value: number): string {
  if (value === 0) return 'cero'

  const parts: string[] = []
  const millions = Math.floor(value / 1_000_000)
  const thousands = Math.floor((value % 1_000_000) / 1000)
  const rest = value % 1000

  if (millions > 0) {
    parts.push(millions === 1 ? 'un millón' : `${esApocope(esUnder1000(millions))} millones`)
  }
  if (thousands > 0) {
    parts.push(thousands === 1 ? 'mil' : `${esApocope(esUnder1000(thousands))} mil`)
  }
  if (rest > 0) parts.push(esUnder1000(rest))

  return parts.join(' ')
}

/** Renders a non-negative amount (USD) as words, e.g. 1234.56 -> "… dollars and fifty-six cents". */
export function amountInWords(value: number, locale: string): string {
  const spanish = locale.toLowerCase().startsWith('es')
  const rounded = Math.round(Math.abs(value) * 100)
  const major = Math.floor(rounded / 100)
  const minor = rounded % 100

  const majorWords = spanish ? esApocope(esInteger(major)) : enInteger(major)
  const majorUnit = spanish
    ? major === 1 ? 'dólar' : 'dólares'
    : major === 1 ? 'dollar' : 'dollars'

  if (minor === 0) return `${majorWords} ${majorUnit}`

  const minorWords = spanish ? esInteger(minor) : enInteger(minor)
  const minorUnit = spanish
    ? minor === 1 ? 'centavo' : 'centavos'
    : minor === 1 ? 'cent' : 'cents'
  const conjunction = spanish ? 'con' : 'and'

  return `${majorWords} ${majorUnit} ${conjunction} ${minorWords} ${minorUnit}`
}
