## Goal
Implement the Payment Entry & Receivable/Payable Settlement phase (R-12) per `.specify/modules/r-12-payment-entry/spec.md`, creating the PaymentEntry voucher to settle open Sales Invoices (Receive) and Purchase Invoices (Pay) against Bank Accounts, closing the accounting loop left open by the 05-banking staging module. This includes gapless fiscal numbering, lifecycle management (Draft → Submitted → Cancelled), General Ledger settlement, invoice outstanding tracking, and advances (UnallocatedAmount).

## Discoveries
- UPDLOCK/HOLDLOCK voucher numbering works correctly inside the ambient posting transaction; rolled-back submissions consume no number.
- RowVersion compare-and-swap on submit/cancel throws concurrency_conflict 409 when stale token detected.
- Idempotent submit returns the first response body verbatim when the same IdempotencyKey is reused.
- Cancel reverses GL rows by swapping Debit/Credit and setting IsCancelled=true while preserving the original PaymentDate.
- Directional guard EnsureDirection() enforces Receive ↔ Customer and Pay ↔ Supplier before any row exists; violations throw payment_party_mismatch error code.
- AllocatedAmount > 0 and exactly-one invoice (SalesInvoiceId XOR PurchaseInvoiceId) constraints are enforced at DB level via check constraints.

## Accomplished
- **Backend entities**: PaymentEntry (with PaymentType, PaymentPartyType, DocumentStatus, VoucherNo, PaidAmount, UnallocatedAmount, ClearanceDate, RowVersion), PaymentAllocation (with SalesInvoiceId/PurchaseInvoiceId exactly-one check, AllocatedAmount).
- **Invariants**: EnsureDirection(), Allocate(), EnsureConservation(), Submit(), Cancel() with full BankingValidationException codes mapped to RFC 7807 ProblemDetails.
- **GL posting**: PaymentPosting.BuildLedgerLines() for submit (Dr bank/Cr receivable or Dr payable/Cr bank) and cancel (swapped Debit/Credit, IsCancelled=true).
- **Controllers**: PaymentEntriesController with GET (list + detail), POST Create (Draft), POST {id}/submit (idempotent), POST {id}/cancel (idempotent).
- **Queries**: GetPaymentsQuery, GetPaymentDetailQuery, GetOutstandingSalesInvoicesQuery, GetOutstandingPurchaseInvoicesQuery.
- **BankAccount CRUD**: BankAccountsController with GET, POST, PUT, {id}/disable|enable; BankAccountDto, CreateBankAccountCommand, UpdateBankAccountCommand.
- **Currency catalog**: CurrenciesController with GET, POST, PUT, {id}/disable|enable; 12 ISO seeds.
- **Outstanding invoices**: SalesInvoiceRepository.GetOutstandingByCustomerAsync, PurchaseRepository.GetOutstandingBySupplierAsync with OutstandingAmount > 0 + Unpaid|PartiallyPaid filter, ordered by due date.
- **Frontend**: usePaymentEntries, useSubmitPaymentEntry, CurrencyFormModal, CurrenciesView, BankAccountFormModal, BankAccountsView, nav routes accounting-currencies + banking-accounts, i18n en/es for all new labels.
- **Migrations**: New migration 20261007141803_PaymentEntrySettlement.cs adds DocumentStatus, PartyId, PartyType, UnallocatedAmount, VoucherNo to PaymentEntry; SalesInvoiceId/PurchaseInvoiceId on PaymentAllocation; check constraints for direction, amount positivity, and exactly-one invoice.
- **All 502 unit tests pass** (Erp.Application.UnitTests, Release build, 0 errors).

## Next Steps
1. Run `dotnet build src/Backend/Erp.Api/Erp.Api.csproj -c Release` and `npm run build` in `src/Frontend/erp-client` to confirm zero errors.
2. Execute integration test suite against SQL Server (`dotnet test tests/Erp.Api.IntegrationTests/Erp.Api.IntegrationTests.csproj -c Release`) to verify endpoint round-trips, idempotency replay, and outstanding-invoice grid data.
3. Verify DoubleEntryGuard.EnsureBalanced and FiscalPeriodLockedException gates in submit/cancel against frozen periods.
4. Wire frontend PaymentEntry detail page and allocation grid integration against outstanding-invoices endpoint (pending onRowClick + handleSave in views).

## Relevant Files
- **Backend domain**: `src/Backend/Erp.Domain/Entities/PaymentEntry.cs`, `PaymentAllocation.cs`, `BankingErrorCodes.cs`
- **Backend configs**: `src/Backend/Erp.Infrastructure/Data/Configurations/PaymentEntryConfiguration.cs`, `PaymentAllocationConfiguration.cs`
- **Backend repos**: `src/Backend/Erp.Domain/Repositories/IBankRepository.cs`, `BankRepository.cs`, `SalesInvoiceRepository.cs`, `PurchaseRepository.cs`
- **Backend commands/handlers**: `src/Backend/Erp.Application/Features/Payments/Commands/` (Create/Submit/Cancel + inputs), `src/Backend/Erp.Application/Features/Payments/Queries/` (GetPayments/GetPaymentDetail/GetOutstanding)
- **Backend controllers**: `src/Backend/Erp.Api/Controllers/V1/PaymentEntriesController.cs`
- **Backend DTOs**: `src/Backend/Erp.Application/DTOs/PaymentEntryDto.cs`, `PaymentAllocationDto.cs`, `OutstandingInvoiceDto.cs`
- **Backend posting**: `src/Backend/Erp.Application/Features/Payments/PaymentPosting.cs`
- **Frontend hooks**: `src/Frontend/erp-client/src/features/payments/api/usePaymentEntries.ts`, `useSubmitPaymentEntry.ts`
- **Frontend components**: `src/Frontend/erp-client/src/features/payments/CurrenciesView.tsx`, `BankAccountsView.tsx`, `CurrencyFormModal.tsx`, `BankAccountFormModal.tsx`
- **Frontend types**: `src/Frontend/erp-client/src/features/payments/types.ts` (PaymentEntry, PaymentAllocation, OutstandingInvoice, DocumentStatus, PaymentType, PaymentPartyType)
- **Frontend nav**: `src/Frontend/erp-client/src/store/useNavigationStore.ts` (added accounting-currencies, banking-accounts); `src/Frontend/erp-client/src/components/layout/Sidebar.tsx`; `src/Frontend/erp-client/src/components/layout/Header.tsx`
- **Frontend locales**: `src/Frontend/erp-client/public/locales/en/accounting.json`, `es/accounting.json`, `en/banking.json`, `es/banking.json`, `en/common.json`, `es/common.json`
- **Tests**: `tests/Erp.Application.UnitTests/CreatePaymentEntryCommandHandlerTests.cs`, `SubmitPaymentEntryCommandHandlerTests.cs`, `CancelPaymentEntryCommandHandlerTests.cs`, `FakeCurrencyRepository.cs`, `FakeBankRepository.cs`, `FakeSalesInvoiceRepository.cs`