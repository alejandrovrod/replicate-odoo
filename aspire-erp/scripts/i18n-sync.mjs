#!/usr/bin/env node
/**
 * Keeps the frontend error catalog in lockstep with the backend resx (spec 00-i18n, task F-03).
 *
 * Reads `src/Backend/Erp.Api/Resources/Shared/ErrorMessages{,.es}.resx` and writes
 * `src/Frontend/erp-client/public/locales/{en,es}/error.json`. Regenerating is the ONLY
 * supported way to edit those two files - hand edits drift from the wire contract on the next
 * run, and the backend keeps `error.code` as the source of truth.
 *
 * Then, from every shipped `en` namespace, emits `src/types/i18n.generated.d.ts` so a `t()`
 * call with a key that does not exist in English fails `tsc -b` (spec task F-05 / F-23).
 *
 * Usage:
 *   node scripts/i18n-sync.mjs          # regenerate catalogs + types
 *   node scripts/i18n-sync.mjs --check  # exit 1 if anything is out of date (CI gate)
 */
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join, basename, resolve } from 'node:path'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const check = process.argv.includes('--check')

const RESX_DIR = join(root, 'src/Backend/Erp.Api/Resources/Shared')
const LOCALES_DIR = join(root, 'src/Frontend/erp-client/public/locales')
const TYPES_OUT = join(root, 'src/Frontend/erp-client/src/types/i18n.generated.d.ts')
const EN_DIR = join(LOCALES_DIR, 'en')

const ENTITIES = { '&amp;': '&', '&lt;': '<', '&gt;': '>', '&quot;': '"', '&apos;': "'" }
const decode = (s) =>
  s
    .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(Number(d)))
    .replace(/&(amp|lt|gt|quot|apos);/g, (m) => ENTITIES[m])

/** Parses a .resx into an ordered `{ name: value }` map (insertion order = file order). */
function parseResx(file) {
  if (!existsSync(file)) throw new Error(`Missing resx: ${file}`)
  const xml = readFileSync(file, 'utf8')
  const out = {}
  for (const m of xml.matchAll(
    /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>\s*<\/data>/g,
  )) {
    out[m[1]] = decode(m[2])
  }
  if (Object.keys(out).length === 0) throw new Error(`No <data> entries parsed from ${file}`)
  return out
}

const enErrors = parseResx(join(RESX_DIR, 'ErrorMessages.resx'))
const esErrors = parseResx(join(RESX_DIR, 'ErrorMessages.es.resx'))

const missingEs = Object.keys(enErrors).filter((k) => !(k in esErrors))
const orphanEs = Object.keys(esErrors).filter((k) => !(k in enErrors))
if (missingEs.length || orphanEs.length) {
  const parts = []
  if (missingEs.length) parts.push(`${missingEs.length} key(s) without Spanish: ${missingEs.slice(0, 5).join(', ')}`)
  if (orphanEs.length) parts.push(`${orphanEs.length} Spanish key(s) not in en: ${orphanEs.slice(0, 5).join(', ')}`)
  throw new Error(`ErrorMessages en/es key parity broken - ${parts.join(' | ')}`)
}

const enErrorsSorted = Object.fromEntries(Object.keys(enErrors).sort().map((k) => [k, enErrors[k]]))
const esErrorsSorted = Object.fromEntries(Object.keys(esErrors).sort().map((k) => [k, esErrors[k]]))

/** Builds `{ flat: {...}, sections: [...] }` ready for JSON.stringify with 2-space indent. */
function toJson(map) {
  return `${JSON.stringify(map, null, 2)}\n`
}

const outputs = [
  [join(LOCALES_DIR, 'en/error.json'), toJson(enErrorsSorted)],
  [join(LOCALES_DIR, 'es/error.json'), toJson(esErrorsSorted)],
]

// ---- type generation from every shipped `en` namespace -------------------------------
function collectKeys(value, prefix = '') {
  const keys = []
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    for (const [k, v] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${k}` : k
      if (v && typeof v === 'object' && !Array.isArray(v)) keys.push(...collectKeys(v, path))
      else keys.push(path)
    }
  }
  return keys
}

function buildTypes(errorNamespace) {
  if (!existsSync(EN_DIR)) throw new Error(`Missing ${EN_DIR}`)
  const files = readdirSync(EN_DIR).filter((f) => f.endsWith('.json'))
  if (files.length === 0) throw new Error('No en namespace JSON files found')

  const nsToType = new Map()
  const blocks = []

  for (const file of files.sort()) {
    const ns = basename(file, '.json')
    const pascal = ns
      .split(/[^a-z0-9]+/i)
      .filter(Boolean)
      .map((part) => part[0].toUpperCase() + part.slice(1))
      .join('')
    const typeName = `En${pascal}Keys`
    // `error` is sourced from the resx, not from disk, so --check stays deterministic even
    // when error.json itself is the stale file being reported.
    const contents =
      file === 'error.json' ? errorNamespace : JSON.parse(readFileSync(join(EN_DIR, file), 'utf8'))
    const keys = collectKeys(contents)
    if (keys.length === 0) throw new Error(`Namespace ${ns} has no keys`)
    const union = keys.map((k) => `  | ${JSON.stringify(k)}`).join('\n')
    blocks.push(`export type ${typeName} =\n${union}\n`)
    nsToType.set(ns, typeName)
  }

  const resources = [...nsToType.entries()]
    .map(([ns, type]) => `    ${JSON.stringify(ns)}: Record<${type}, string>`)
    .join('\n')

  return `// AUTO-GENERATED by scripts/i18n-sync.mjs - DO NOT EDIT.
// Source of truth: src/Frontend/erp-client/public/locales/en/*.json
// A \`t()\` call with a key absent from English fails \`tsc -b\` (spec 00-i18n F-05 / F-23).

${blocks.join('\n')}
export interface GeneratedResources {
${resources}
}
`
}

outputs.push([TYPES_OUT, buildTypes(enErrorsSorted)])

// ---- write or verify -----------------------------------------------------------------
let stale = []
for (const [path, content] of outputs) {
  const current = existsSync(path) ? readFileSync(path, 'utf8') : null
  if (current === content) continue
  stale.push(path.slice(root.length + 1))
  if (!check) writeFileSync(path, content, 'utf8')
}

if (check && stale.length > 0) {
  console.error(`i18n-sync --check FAILED, ${stale.length} file(s) out of date:`)
  for (const p of stale) console.error(`  ${p}`)
  console.error('Run: node scripts/i18n-sync.mjs')
  process.exit(1)
}

console.log(
  check
    ? `i18n-sync --check OK (${Object.keys(enErrors).length} error codes, ${outputs.length} files)`
    : `i18n-sync wrote ${stale.length} file(s) (${Object.keys(enErrors).length} error codes)`,
)
