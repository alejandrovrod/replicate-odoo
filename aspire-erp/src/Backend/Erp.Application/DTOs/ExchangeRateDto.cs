namespace Erp.Application.DTOs;

public class ExchangeRateDto
{
    public Guid Id { get; set; }
    public Guid FromCurrencyId { get; set; }
    public Guid ToCurrencyId { get; set; }
    public DateOnly RateDate { get; set; }
    public decimal Rate { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
