namespace Erp.Application.DTOs;

public record ExchangeRateRevaluationDto(
    Guid Id, 
    Guid CompanyId, 
    string VoucherNo, 
    DateOnly PostingDate,
    Guid? ExchangeGainLossAccountId, 
    decimal RoundingLossAllowance, 
    string DocumentStatus,
    string? Remarks, 
    byte[] RowVersion);

public record ExchangeRateRevaluationLineDto(
    Guid AccountId, 
    string AccountCode, 
    decimal BalanceInAccountCurrency, 
    decimal BalanceInCompanyCurrency,
    decimal CurrentExchangeRate, 
    decimal NewExchangeRate, 
    decimal NewBalanceInCompanyCurrency, 
    decimal GainLoss, 
    bool ZeroBalance);
    
public record ExchangeRateRevaluationDetailDto(
    ExchangeRateRevaluationDto Revaluation,
    List<ExchangeRateRevaluationLineDto> Lines);
