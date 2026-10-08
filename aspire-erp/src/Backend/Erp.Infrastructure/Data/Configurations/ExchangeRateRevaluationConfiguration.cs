using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

public class ExchangeRateRevaluationConfiguration : IEntityTypeConfiguration<ExchangeRateRevaluation>
{
    public void Configure(EntityTypeBuilder<ExchangeRateRevaluation> builder)
    {
        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ExchangeGainLossAccount)
            .WithMany()
            .HasForeignKey(e => e.ExchangeGainLossAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.RoundingLossAllowance).HasPrecision(18, 6);
    }
}

public class ExchangeRateRevaluationLineConfiguration : IEntityTypeConfiguration<ExchangeRateRevaluationLine>
{
    public void Configure(EntityTypeBuilder<ExchangeRateRevaluationLine> builder)
    {
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.BalanceInForeignCurrency).HasPrecision(18, 6);
        builder.Property(e => e.BalanceInBaseCurrency).HasPrecision(18, 6);
        builder.Property(e => e.CurrentExchangeRate).HasPrecision(18, 6);
        builder.Property(e => e.NewExchangeRate).HasPrecision(18, 6);
        builder.Property(e => e.GainLossAmount).HasPrecision(18, 6);
    }
}
