using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>
/// Journal Entry line: decimal(18,4) money with the SAME non-negative CHECK constraints the
/// ledger carries (Constitution IV.3 - <c>CK_Debit_NonNegative</c> / <c>CK_Credit_NonNegative</c>
/// mirrored here as <c>CK_JournalEntryLine_Debit_NonNegative</c> /
/// <c>CK_JournalEntryLine_Credit_NonNegative</c>), so a negative amount can never even reach the
/// draft - and therefore can never be copied into GLEntry on submit.
/// </summary>
public sealed class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLine", table =>
        {
            table.HasCheckConstraint(
                "CK_JournalEntryLine_Debit_NonNegative", "[Debit] >= 0");
            table.HasCheckConstraint(
                "CK_JournalEntryLine_Credit_NonNegative", "[Credit] >= 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(l => l.LineNumber).IsRequired();
        builder.Property(l => l.Debit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(l => l.Credit).HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(l => l.PartyType).HasMaxLength(50);

        // plan.md §2 GLEntry shape: the account FK is Restrict on BOTH tables, so a draft (or a
        // posted voucher) never loses its account to a cascade delete.
        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .HasConstraintName("FK_JournalEntryLine_Account")
            .OnDelete(DeleteBehavior.Restrict);

        // Ordered line read when the aggregate is materialized.
        builder.HasIndex(l => new { l.JournalEntryId, l.LineNumber })
            .HasDatabaseName("IX_JournalEntryLine_JournalEntry_Line");
    }
}
