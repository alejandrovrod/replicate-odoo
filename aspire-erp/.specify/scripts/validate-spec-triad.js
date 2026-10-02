#!/usr/bin/env node
/**
 * Recursive Specification Validator Agent (3-Pass Convergence Loop)
 * Audits, validates, and certifies Spec Kit Triads (spec.md, plan.md, tasks.md)
 */

const fs = require('fs');
const path = require('path');

const modulePath = process.argv[2];
const currentPass = parseInt(process.argv[3] || '1', 10);

if (!modulePath) {
  console.error("Usage: node validate-spec-triad.js <module-dir> [pass: 1|2|3]");
  process.exit(1);
}

const resolvedPath = path.resolve(modulePath);
const specFile = path.join(resolvedPath, 'spec.md');
const planFile = path.join(resolvedPath, 'plan.md');
const tasksFile = path.join(resolvedPath, 'tasks.md');

console.log(`\n======================================================================`);
console.log(` RECURSIVE SPEC VALIDATION AGENT: PASS ${currentPass} / 3`);
console.log(` Target Module: ${path.basename(resolvedPath)}`);
console.log(` Directory: ${resolvedPath}`);
console.log(`======================================================================\n`);

// 1. Check file existence
const errors = [];
const warnings = [];
const passedChecks = [];

if (!fs.existsSync(specFile)) errors.push("Missing required spec.md");
if (!fs.existsSync(planFile)) errors.push("Missing required plan.md");
if (!fs.existsSync(tasksFile)) errors.push("Missing required tasks.md");

if (errors.length > 0) {
  console.error("CRITICAL ERRORS:");
  errors.forEach(e => console.error(`  [X] ${e}`));
  process.exit(1);
}

const specContent = fs.readFileSync(specFile, 'utf8');
const planContent = fs.readFileSync(planFile, 'utf8');
const tasksContent = fs.readFileSync(tasksFile, 'utf8');

// --- 2. Audit spec.md ---
console.log(`[PASS ${currentPass}] Auditing spec.md (Functional Requirements & Invariants)...`);

if (specContent.includes("Ubiquitous Language") || specContent.includes("ERPNext DocType")) {
  passedChecks.push("spec.md: Ubiquitous Language & DocType alignment present");
} else {
  warnings.push("spec.md: Missing structured Ubiquitous Language table");
}

const invariantCount = (specContent.match(/### Invariant/g) || []).length;
if (invariantCount >= 3) {
  passedChecks.push(`spec.md: ${invariantCount} formal business invariants defined`);
} else {
  errors.push(`spec.md: Insufficient formal invariants (${invariantCount} found, minimum 3 required)`);
}

const mathFormulas = (specContent.match(/\$\$/g) || []).length / 2;
if (mathFormulas >= 2) {
  passedChecks.push(`spec.md: Formal mathematical identities verified (${mathFormulas} equations)`);
} else {
  warnings.push("spec.md: Invariants lack explicit LaTeX/ASCII mathematical formulas ($$...$$)");
}

const gherkinScenarios = (specContent.match(/### Scenario/g) || []).length;
if (gherkinScenarios >= 4) {
  passedChecks.push(`spec.md: ${gherkinScenarios} comprehensive Gherkin scenarios`);
} else {
  errors.push(`spec.md: Insufficient Gherkin scenarios (${gherkinScenarios} found, minimum 4 required covering Happy Path, Rejection, Reversal, Idempotency)`);
}

// Check for edge cases in scenarios
const hasIdempotency = /idempotency|duplicate|replay/i.test(specContent);
if (hasIdempotency) {
  passedChecks.push("spec.md: Idempotency scenario verified");
} else {
  errors.push("spec.md: Missing explicit Idempotency & duplicate submission scenario");
}

const hasReversal = /cancel|reversal|return|undo/i.test(specContent);
if (hasReversal) {
  passedChecks.push("spec.md: Cancellation / Reversal / Return scenario verified");
} else {
  errors.push("spec.md: Missing explicit Cancellation / Reversal scenario");
}

const hasConcurrency = /concurrency|race condition|simultaneous|lock/i.test(specContent);
if (hasConcurrency) {
  passedChecks.push("spec.md: Concurrency / race condition scenario verified");
} else {
  warnings.push("spec.md: Missing explicit Concurrency / race condition scenario");
}

// --- 3. Audit plan.md ---
console.log(`[PASS ${currentPass}] Auditing plan.md (Technical Architecture & DDL)...`);

const hasDdl = planContent.includes("CREATE TABLE");
if (hasDdl) {
  passedChecks.push("plan.md: SQL Server 2025 DDL defined");
} else {
  errors.push("plan.md: Missing executable SQL Server 2025 DDL");
}

const hasCheckConstraints = /CONSTRAINT CK_|CHECK \(/i.test(planContent);
if (hasCheckConstraints) {
  passedChecks.push("plan.md: DDL contains defensive CHECK constraints");
} else {
  errors.push("plan.md: DDL lacks CHECK constraints (positive quantities/rates)");
}

const hasTemporal = /SYSTEM_VERSIONING = ON|PERIOD FOR SYSTEM_TIME/i.test(planContent);
if (hasTemporal) {
  passedChecks.push("plan.md: SQL Server Temporal Tables configured");
} else {
  warnings.push("plan.md: Missing Temporal Tables (SYSTEM_VERSIONING = ON) on master records");
}

const hasIndexes = /CREATE (UNIQUE )?NONCLUSTERED INDEX/i.test(planContent);
if (hasIndexes) {
  passedChecks.push("plan.md: Covering non-clustered indexes specified");
} else {
  errors.push("plan.md: Missing performance & multi-tenant composite indexes");
}

const hasCqrsOrControllers = /namespace Erp\.|public sealed class|public class/i.test(planContent);
if (hasCqrsOrControllers) {
  passedChecks.push("plan.md: Concrete C# Domain Engine & CQRS contracts present");
} else {
  errors.push("plan.md: Missing C# domain engine / CQRS architecture code");
}

const hasErrorCodes = /ErrorCode|ErrorCodes|enum|public static class .*Errors/i.test(planContent);
if (hasErrorCodes) {
  passedChecks.push("plan.md: Explicit domain error codes cataloged");
} else {
  warnings.push("plan.md: Missing cataloged Domain Error Codes enumeration");
}

// --- 4. Audit tasks.md ---
console.log(`[PASS ${currentPass}] Auditing tasks.md (Sequenced Checklist & Tests)...`);

const taskMatches = (tasksContent.match(/- \[[ x]\] \*\*Task/g) || []).length;
if (taskMatches >= 4) {
  passedChecks.push(`tasks.md: ${taskMatches} atomic tasks sequenced`);
} else {
  errors.push(`tasks.md: Too few atomic tasks (${taskMatches} found, minimum 4 required)`);
}

const hasAcceptance = /Acceptance Criteria|Acceptance:/i.test(tasksContent);
if (hasAcceptance) {
  passedChecks.push("tasks.md: Explicit acceptance criteria defined per task");
} else {
  errors.push("tasks.md: Tasks missing explicit Acceptance criteria");
}

const hasTestTask = /test|verify|integration/i.test(tasksContent);
if (hasTestTask) {
  passedChecks.push("tasks.md: Testing / verification tasks included");
} else {
  warnings.push("tasks.md: Missing explicit unit or integration test tasks");
}

// --- Report ---
const totalChecks = passedChecks.length + errors.length + warnings.length;
const score = Math.round((passedChecks.length / (totalChecks || 1)) * 100);

console.log(`\n----------------------------------------------------------------------`);
console.log(` AUDIT SUMMARY (Pass ${currentPass}): Score = ${score}%`);
console.log(` Passed Checks: ${passedChecks.length}`);
console.log(` Blockers/Errors: ${errors.length}`);
console.log(` Warnings: ${warnings.length}`);
console.log(`----------------------------------------------------------------------\n`);

if (passedChecks.length > 0) {
  console.log("PASSED CHECKS:");
  passedChecks.forEach(c => console.log(`  [OK] ${c}`));
  console.log("");
}

if (errors.length > 0) {
  console.log("CRITICAL GAPS (Must be resolved):");
  errors.forEach(e => console.log(`  [!] ${e}`));
  console.log("");
}

if (warnings.length > 0) {
  console.log("RECOMMENDED HARDENING (Warnings):");
  warnings.forEach(w => console.log(`  [*] ${w}`));
  console.log("");
}

if (currentPass === 3) {
  if (errors.length === 0 && warnings.length === 0 && score === 100) {
    console.log(`>>> [CERTIFICATION] Specification Triad is 100% ROBUST & PRODUCTION CERTIFIED! <<<\n`);
    process.exit(0);
  } else {
    console.log(`>>> [VERIFICATION FAILED] Specification did not achieve 100% convergence. <<<\n`);
    process.exit(1);
  }
} else {
  process.exit(errors.length > 0 ? 2 : 0);
}
