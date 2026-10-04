import { useState } from 'react'
import { FileUp, UploadCloud } from 'lucide-react'
import { apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { useTenantStore } from '../../store/useTenantStore'
import type { BankStatementImportSummary } from './types'

type StatementFormat = 'CSV' | 'OFX'

interface ImportPayload {
  bankAccountId: string
  fileName: string
  format: StatementFormat
  content: string
}

/**
 * Drag-and-drop statement importer (task 6.6): the file is read client-side as text and
 * POSTed to `/api/v1/bank-statement-imports` (`ImportBankStatementRequest`). The result
 * panel shows the BN-05 summary (total / imported / duplicates).
 */
export function BankStatementImporter({ onImported }: { onImported: () => void }) {
  const companyId = useTenantStore((state) => state.companyId)
  const [bankAccountId, setBankAccountId] = useState('')
  const [fileName, setFileName] = useState('')
  const [format, setFormat] = useState<StatementFormat>('CSV')
  const [content, setContent] = useState('')
  const [isDragging, setIsDragging] = useState(false)
  const [fileError, setFileError] = useState<string | null>(null)

  const { state: importState, dispatch: runImport, isPending } = useErpAction<
    BankStatementImportSummary,
    ImportPayload
  >(async (payload) => {
    const response = await apiClient.post<BankStatementImportSummary>(
      '/v1/bank-statement-imports',
      {
        companyId,
        bankAccountId: payload.bankAccountId,
        fileName: payload.fileName,
        format: payload.format,
        content: payload.content,
      },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    )
    return response.data
  })

  const readFile = (file: File) => {
    setFileError(null)
    const lower = file.name.toLowerCase()
    if (lower.endsWith('.ofx') || lower.endsWith('.qfx')) setFormat('OFX')
    else if (lower.endsWith('.csv')) setFormat('CSV')
    const reader = new FileReader()
    reader.onload = () => {
      setContent(typeof reader.result === 'string' ? reader.result : '')
      setFileName(file.name)
    }
    reader.onerror = () => setFileError('Could not read the file as text.')
    reader.readAsText(file)
  }

  const canSubmit =
    companyId !== '' && bankAccountId.trim() !== '' && content !== '' && !isPending

  const submit = () => {
    if (!canSubmit) return
    runImport({ bankAccountId: bankAccountId.trim(), fileName: fileName || 'statement', format, content })
  }

  if (importState.isSuccess && importState.data) {
    const summary = importState.data
    return (
      <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-5">
        <h3 className="text-sm font-bold text-emerald-900">
          Import complete: {summary.fileName}
        </h3>
        <div className="mt-3 grid grid-cols-3 gap-3 text-center">
          <div className="rounded-lg bg-white p-3">
            <p className="text-xl font-bold text-slate-900">{summary.totalTransactions}</p>
            <p className="text-[11px] text-slate-500">Total rows</p>
          </div>
          <div className="rounded-lg bg-white p-3">
            <p className="text-xl font-bold text-emerald-700">{summary.importedCount}</p>
            <p className="text-[11px] text-slate-500">Imported</p>
          </div>
          <div className="rounded-lg bg-white p-3">
            <p className="text-xl font-bold text-amber-700">{summary.duplicateCount}</p>
            <p className="text-[11px] text-slate-500">Duplicates (FITID)</p>
          </div>
        </div>
        <button
          type="button"
          onClick={onImported}
          className="mt-4 rounded-lg bg-emerald-600 px-3 py-2 text-xs font-semibold text-white hover:bg-emerald-700"
        >
          Back to workbench
        </button>
      </div>
    )
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
      <h3 className="text-sm font-bold text-slate-900">Import bank statement</h3>
      <p className="mt-1 text-xs text-slate-500">
        Staged only: zero accounting entries are posted until reconciliation (BN-01).
      </p>

      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="block text-xs font-semibold text-slate-700">
          Bank account id
          <input
            value={bankAccountId}
            onChange={(e) => setBankAccountId(e.target.value)}
            placeholder="BankAccount GUID"
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-mono text-xs font-normal"
          />
        </label>
        <label className="block text-xs font-semibold text-slate-700">
          Format
          <select
            value={format}
            onChange={(e) => setFormat(e.target.value === 'OFX' ? 'OFX' : 'CSV')}
            className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-normal"
          >
            <option value="CSV">CSV</option>
            <option value="OFX">OFX 1.x</option>
          </select>
        </label>
      </div>

      <div
        onDragOver={(e) => {
          e.preventDefault()
          setIsDragging(true)
        }}
        onDragLeave={() => setIsDragging(false)}
        onDrop={(e) => {
          e.preventDefault()
          setIsDragging(false)
          const file = e.dataTransfer.files[0]
          if (file) readFile(file)
        }}
        className={`mt-4 flex flex-col items-center justify-center rounded-xl border-2 border-dashed px-6 py-8 text-center ${
          isDragging ? 'border-sky-500 bg-sky-50' : 'border-slate-300 bg-slate-50'
        }`}
      >
        <UploadCloud className="h-8 w-8 text-sky-600" />
        <p className="mt-2 text-sm font-semibold text-slate-700">
          {fileName ? <span className="font-mono">{fileName}</span> : 'Drop a CSV / OFX file here'}
        </p>
        <p className="mt-1 text-xs text-slate-500">or</p>
        <label className="mt-2 cursor-pointer rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50">
          Browse files
          <input
            type="file"
            accept=".csv,.ofx,.qfx"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) readFile(file)
            }}
          />
        </label>
        {content !== '' && (
          <p className="mt-2 font-mono text-[11px] text-slate-500">
            {(content.length / 1024).toFixed(1)} KB ready
          </p>
        )}
      </div>

      {(fileError ?? importState.error) && (
        <div className="mt-3 rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
          {fileError ?? `${importState.error}${importState.errorCode ? ` (${importState.errorCode})` : ''}`}
        </div>
      )}

      <button
        type="button"
        disabled={!canSubmit}
        onClick={submit}
        className="mt-4 flex items-center gap-1.5 rounded-lg bg-sky-600 px-3 py-2 text-xs font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
      >
        <FileUp className="h-4 w-4" />
        {isPending ? 'Importing…' : 'Import statement'}
      </button>
    </div>
  )
}
