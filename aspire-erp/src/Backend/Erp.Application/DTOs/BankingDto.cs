using Erp.Domain.Entities;

namespace Erp.Application.DTOs;

/// <summary>Staging bank line payload for GET /api/v1/bank-transactions (Block B).</summary>
public sealed record BankTransactionDto(
    Guid Id,
    Guid CompanyId,
    Guid BankAccountId,
    DateOnly TransactionDate,
    decimal Deposit,
    decimal Withdrawal,
    string Currency,
    string Description,
    string? ReferenceNumber,
    string? TransactionId,
    string Status,
    decimal AllocatedAmount,
    DateOnly? ClearanceDate,
    string? SuggestedPartyType,
    Guid? SuggestedPartyId,
    Guid? SuggestedAccountId,
    string? RowVersion)
{
    public static BankTransactionDto From(BankTransaction transaction) =>
        new(
            transaction.Id,
            transaction.CompanyId,
            transaction.BankAccountId,
            transaction.TransactionDate,
            transaction.Deposit,
            transaction.Withdrawal,
            transaction.Currency,
            transaction.Description,
            transaction.ReferenceNumber,
            transaction.TransactionId,
            transaction.Status.ToString(),
            transaction.AllocatedAmount,
            transaction.ClearanceDate,
            transaction.SuggestedPartyType,
            transaction.SuggestedPartyId,
            transaction.SuggestedAccountId,
            transaction.RowVersion is null ? null : Convert.ToBase64String(transaction.RowVersion));
}

/// <summary>Heuristic rule payload for GET/POST /api/v1/bank-transaction-rules (Block B/C).</summary>
public sealed record BankTransactionRuleDto(
    Guid Id,
    Guid CompanyId,
    string RuleName,
    int Priority,
    Guid? BankAccountId,
    string ConditionType,
    string Pattern,
    string? TargetPartyType,
    Guid? TargetPartyId,
    bool AutoCreateVoucher,
    Guid? TargetExpenseAccountId,
    bool IsActive)
{
    public static BankTransactionRuleDto From(BankTransactionRule rule) =>
        new(
            rule.Id,
            rule.CompanyId,
            rule.RuleName,
            rule.Priority,
            rule.BankAccountId,
            rule.ConditionType.ToString(),
            rule.Pattern,
            rule.TargetPartyType,
            rule.TargetPartyId,
            rule.AutoCreateVoucher,
            rule.TargetExpenseAccountId,
            rule.IsActive);
}

/// <summary>Import batch summary echoed by POST /api/v1/bank-statement-imports (Block B).</summary>
public sealed record BankStatementImportDto(
    Guid ImportId,
    string FileName,
    int TotalTransactions,
    int ImportedCount,
    int DuplicateCount);

/// <summary>Bank account master payload for GET/POST/PUT /api/v1/bank-accounts.</summary>
public sealed record BankAccountDto(
    Guid Id,
    Guid CompanyId,
    string AccountName,
    string BankName,
    string AccountNumber,
    Guid? CurrencyId,
    Guid GLAccountId,
    decimal LastReconciledBalance,
    DateOnly? LastReconciledDate,
    bool IsActive,
    byte[]? RowVersion = null)
{
    public static BankAccountDto From(BankAccount account) =>
        new(
            account.Id,
            account.CompanyId,
            account.AccountName,
            account.BankName,
            account.AccountNumber,
            account.CurrencyId,
            account.GLAccountId,
            account.LastReconciledBalance,
            account.LastReconciledDate,
            account.IsActive,
            account.RowVersion);
}
