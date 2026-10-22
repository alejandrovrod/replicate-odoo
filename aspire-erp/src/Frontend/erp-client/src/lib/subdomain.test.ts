import { describe, expect, it } from 'vitest'
import {
  getCurrentSubdomain,
  isReservedSubdomain,
  isSubdomainFormatValid,
  isSubdomainValid,
  normalizeSubdomain,
  tenantUrl,
} from './subdomain'

describe('subdomain helpers', () => {
  it('normalizes trim and case', () => {
    expect(normalizeSubdomain('  Acme-Corp  ')).toBe('acme-corp')
  })

  it.each(['acme', 'acme-corp', 'a1b', 'tenant-01', 'a1234567890123456789012345678901'])(
    'accepts legal label %s',
    (value) => {
      expect(isSubdomainFormatValid(value)).toBe(true)
      expect(isSubdomainValid(value)).toBe(true)
    },
  )

  it.each(['ab', '-acme', 'acme-', 'ac_me', 'ac.me', 'a12345678901234567890123456789012'])(
    'rejects malformed label %s',
    (value) => {
      expect(isSubdomainFormatValid(value)).toBe(false)
      expect(isSubdomainValid(value)).toBe(false)
    },
  )

  it.each(['api', 'app', 'admin', 'auth', 'login', 'signup', 'register', 'www', 'mail', 'status', 'dev', 'staging', 'ADMIN'])(
    'rejects reserved label %s',
    (value) => {
      expect(isReservedSubdomain(value)).toBe(true)
      expect(isSubdomainValid(value)).toBe(false)
    },
  )

  it.each([
    ['acme.domain.com', 'acme'],
    ['ACME.Domain.COM', 'acme'],
    ['acme.domain.com:443', 'acme'],
    ['acme.localhost', 'acme'],
    ['acme.localhost:5173', 'acme'],
  ])('extracts %s', (host, expected) => {
    expect(getCurrentSubdomain(host)).toBe(expected)
  })

  it('honors a custom base domain', () => {
    expect(getCurrentSubdomain('erp.example.org', 'example.org')).toBe('erp')
  })

  it.each(['localhost', 'domain.com', '127.0.0.1', 'api.domain.com', 'a.b.domain.com', 'unrelated.example.com', ''])(
    'returns null for %s',
    (host) => {
      expect(getCurrentSubdomain(host)).toBeNull()
    },
  )

  it('builds a tenant URL for the current environment', () => {
    // jsdom serves from localhost, so the helper keeps the current port for local development.
    expect(tenantUrl('acme')).toMatch(/^https?:\/\/acme\.(localhost|domain\.com)(:\d+)?$/)
  })
})
