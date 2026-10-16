import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import i18n from './i18n'
import { apiClient } from '../api/client'

/** Active preset payload (mirrors the overlay endpoint source). */
export interface ActivePreset {
  presetKey: string | null
  name: string | null
  /** language -> namespace -> key -> override. */
  terminology: Record<string, Record<string, Record<string, string>>>
}

interface TerminologyContextValue {
  presetKey: string | null
  presetName: string | null
  /** False until the active preset has been read (overlay terms applied or confirmed absent). */
  ready: boolean
  reload: () => void
}

const TerminologyContext = createContext<TerminologyContextValue>({
  presetKey: null,
  presetName: null,
  ready: false,
  reload: () => {},
});

/**
 * Tenant terminology overlay (workflow Fase 5, gap D4): applies the active industry
 * preset's vocabulary OVER the shipped catalogs at runtime via
 * `i18n.addResourceBundle(lng, ns, terms, deep, overwrite)` - e.g. a clinic reads
 * "Pacientes" where the catalog says "Customers".
 *
 * Cero mutación del wire-contract: DTOs, endpoints and tables stay 100% canonical -
 * only rendered text changes, per language, per namespace. Applying a different preset
 * reloads the page (bundles merge additively; a reload guarantees no stale terms).
 */
export function TerminologyOverlayProvider({ children }: { children: ReactNode }) {
  const [value, setValue] = useState<TerminologyContextValue>({
    presetKey: null,
    presetName: null,
    ready: false,
    reload: () => {},
  })

  const load = useCallback(async (signal: { cancelled: boolean }) => {
    try {
      const response = await apiClient.get<ActivePreset>('/v1/presets/active')
      if (signal.cancelled) return
      const { presetKey, name, terminology } = response.data
      const namespacesToEnsure = new Set<string>()
      for (const namespaces of Object.values(terminology ?? {})) {
        for (const ns of Object.keys(namespaces ?? {})) {
          namespacesToEnsure.add(ns)
        }
      }
      if (namespacesToEnsure.size > 0) {
        await i18n.loadNamespaces(Array.from(namespacesToEnsure))
      }

      let hasUpdates = false
      for (const [lng, namespaces] of Object.entries(terminology ?? {})) {
        for (const [ns, terms] of Object.entries(namespaces ?? {})) {
          if (terms && Object.keys(terms).length > 0) {
            // Expand dotted keys like "nav.item.sellingCustomers" into nested objects { nav: { item: { sellingCustomers } } }
            // so i18next deep merge (deep=true) replaces the exact leaf in the bundle!
            const nested: Record<string, any> = {}
            for (const [key, val] of Object.entries(terms)) {
              const parts = key.split('.')
              let current = nested
              for (let i = 0; i < parts.length - 1; i++) {
                current[parts[i]] = current[parts[i]] || {}
                current = current[parts[i]]
              }
              current[parts[parts.length - 1]] = val
            }
            i18n.addResourceBundle(lng, ns, nested, true, true)
            hasUpdates = true
          }
        }
      }
      if (hasUpdates) {
        // Emit change so all useTranslation() hooks re-render with the new overlay keys
        i18n.emit('added')
        await i18n.changeLanguage(i18n.language)
      }
      setValue((prev) => ({ ...prev, presetKey, presetName: name, ready: true }))
    } catch {
      // No preset (or offline): the shipped catalogs stand alone. Never blocks render.
      if (!signal.cancelled) setValue((prev) => ({ ...prev, ready: true }))
    }
  }, [])

  useEffect(() => {
    const signal = { cancelled: false }
    void load(signal)
    return () => {
      signal.cancelled = true
    }
  }, [load])

  const reload = useCallback(() => {
    const signal = { cancelled: false }
    void load(signal)
  }, [load])

  return (
    <TerminologyContext.Provider value={{ ...value, reload }}>
      {children}
    </TerminologyContext.Provider>
  )
}

export function useTerminology(): TerminologyContextValue {
  return useContext(TerminologyContext)
}
