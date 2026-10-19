import { describe, expect, it } from 'vitest'
import { amountInWords } from './amountInWords'

describe('amountInWords (pay-stub net line)', () => {
  it('renders whole amounts in English', () => {
    expect(amountInWords(0, 'en')).toBe('zero dollars')
    expect(amountInWords(1, 'en')).toBe('one dollar')
    expect(amountInWords(1234, 'en')).toBe('one thousand two hundred thirty-four dollars')
  })

  it('appends cents in English', () => {
    expect(amountInWords(1234.56, 'en')).toBe(
      'one thousand two hundred thirty-four dollars and fifty-six cents',
    )
    expect(amountInWords(0.01, 'en')).toBe('zero dollars and one cent')
  })

  it('renders whole amounts in Spanish, apocopating uno before the noun', () => {
    expect(amountInWords(0, 'es')).toBe('cero dólares')
    expect(amountInWords(1, 'es')).toBe('un dólar')
    expect(amountInWords(21, 'es')).toBe('veintiún dólares')
    expect(amountInWords(31, 'es')).toBe('treinta y un dólares')
    expect(amountInWords(100, 'es')).toBe('cien dólares')
    expect(amountInWords(1234, 'es')).toBe('mil doscientos treinta y cuatro dólares')
  })

  it('appends cents in Spanish', () => {
    expect(amountInWords(1234.56, 'es')).toBe(
      'mil doscientos treinta y cuatro dólares con cincuenta y seis centavos',
    )
  })

  it('apocopates the thousands group in Spanish', () => {
    expect(amountInWords(21000, 'es')).toBe('veintiún mil dólares')
  })

  it('falls back to English for locales it does not know', () => {
    expect(amountInWords(1, 'fr')).toBe('one dollar')
  })

  it('renders the cents component', () => {
    expect(amountInWords(2.5, 'en')).toBe('two dollars and fifty cents')
    expect(amountInWords(2.5, 'es')).toBe('dos dólares con cincuenta centavos')
  })
})
