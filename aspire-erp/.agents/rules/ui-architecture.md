# UI Architecture & Standardization Rules

## 1. Multilanguage (i18n) is Mandatory
- **NO hardcoded UI strings.** Every piece of text shown to the user MUST use `useTranslation()` from `react-i18next`.
- This applies to: Labels, Placeholders, Table Headers, Button text, Select dropdown options, and Error messages.
- Always provide a fallback explicitly: `t('namespace.key', 'Default English Text')`.

## 2. Form Modal Standardization (UX/UI)
- **Do not design "on the fly".** Follow this strict layout for all create/edit modals:
  - **Width:** Use `max-w-lg` for `DialogContent`. Avoid the cramped default `max-w-md`.
  - **Spacing:** The form wrapper must have `className="space-y-6 pt-4"` to ensure breathable vertical rhythm.
  - **Checkboxes:** Standardize them as simple inline checkboxes with spacing.
    ```tsx
    <div className="flex items-center space-x-2">
      <input type="checkbox" className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950" />
      <label className="text-sm font-medium">
        Title <span className="text-slate-500 font-normal">(Helpful description)</span>
      </label>
    </div>
    ```
- Consistently use standard `Input`, `Button`, `Dialog` UI components.
