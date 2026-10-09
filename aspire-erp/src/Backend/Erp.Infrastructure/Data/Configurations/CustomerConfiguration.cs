using Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Erp.Infrastructure.Data.Configurations;

/// <summary>plan.md §1: Customer master with temporal history, the credit CHECK and the composite unique code.</summary>
/// <remarks>
/// plan.md §1's DDL is explicit (<c>WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE =
/// dbo.CustomerHistory))</c>), so Customer is system-versioned exactly like Account/Company
/// (EF 10 hangs temporal config off ToTable(...)). RowVersion (spec SL-06 optimistic concurrency
/// on credit exposure) coexists with the temporal period on the same table - the Account
/// precedent proves it works: rowversion is a regular column, the period stays datetime2.
/// </remarks>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer", table =>
        {
            table.IsTemporal(t => t.UseHistoryTable("CustomerHistory"));

            // plan.md §1 CK_Customer_CreditLimit: the database enforces the non-negative limit
            // behind the CreditControlEvaluator's `CreditLimit <= 0 = no control` reading.
            table.HasCheckConstraint("CK_Customer_CreditLimit", "[CreditLimit] >= 0.0000");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Optimistic concurrency (spec SL-06): store-generated rowversion token, same coexistence
        // with the temporal table as AccountConfiguration.
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.Property(c => c.CustomerCode).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.TaxId).HasMaxLength(50).IsRequired();

        // ERPNext-parity profile columns: all NOT NULL with server defaults so the
        // migration is non-destructive on existing rows (CustomerType keeps the
        // ERPNext "Company" default, the rest default to the empty string).
        builder.Property(c => c.CustomerType).HasMaxLength(50).IsRequired().HasDefaultValue("Company");
        builder.Property(c => c.CustomerGroup).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.Territory).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.BillingAddress).HasMaxLength(500).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.Phone).HasMaxLength(50).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.Email).HasMaxLength(150).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.ContactPerson).HasMaxLength(150).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.Website).HasMaxLength(200).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.PaymentTerms).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(c => c.CustomerDetails).HasMaxLength(1000).IsRequired().HasDefaultValue(string.Empty);

        builder.Property(c => c.DefaultReceivableAccountId);

        // plan.md §1 DDL defaults: CreditLimit 0.0000, BypassCreditLimitCheck 0, 30 days,
        // OutstandingAmount 0.0000, IsActive 1. Currency is RM-09 (nullable FK, "USD" fallback).
        builder.Property(c => c.CreditLimit)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(c => c.BypassCreditLimitCheck).HasDefaultValue(false);
        builder.Property(c => c.CurrencyId);
        builder.Property(c => c.PaymentTermsDays).HasDefaultValue(30);
        builder.Property(c => c.OutstandingAmount)
            .HasColumnType("decimal(18,4)").HasDefaultValue(0m).IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true);

        // plan.md §1 FK_Customer_Company / FK_Customer_Account: plain FK semantics = SQL Server
        // default NO ACTION (EF's convention would silently become ON DELETE CASCADE).
        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .HasConstraintName("FK_Customer_Company")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.DefaultReceivableAccount)
            .WithMany()
            .HasForeignKey(c => c.DefaultReceivableAccountId)
            .HasConstraintName("FK_Customer_Account")
            .OnDelete(DeleteBehavior.Restrict);

        // RM-09: billing currency link to the global Currency catalog (nullable for legacy rows).
        builder.HasOne(c => c.Currency)
            .WithMany()
            .HasForeignKey(c => c.CurrencyId)
            .HasConstraintName("FK_Customer_Currency")
            .OnDelete(DeleteBehavior.Restrict);

        // Constitution Article IV.1 + plan.md §1: TenantId leads the composite index, which is
        // UNIQUE under UQ_Customer_Tenant_Company_Code - the database is the authority for
        // duplicate codes (the handler's ExistsCodeAsync pre-check keeps the 409 UX, and
        // CustomerRepository.AddAsync translates the raced violation back to
        // duplicate_customer_code).
        builder.HasIndex(c => new { c.TenantId, c.CompanyId, c.CustomerCode })
            .IsUnique()
            .HasDatabaseName("UQ_Customer_Tenant_Company_Code");
    }
}
