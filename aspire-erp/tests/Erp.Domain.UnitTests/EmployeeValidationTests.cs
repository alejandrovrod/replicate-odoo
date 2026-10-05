using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// Task 12.1 acceptance ("active employees have valid email and bank accounts") plus the
/// department tree guards: email/bank rules, relieving-date order, the self-parent and
/// multi-level cycle. Uniqueness note: employee-number uniqueness is DB-enforced
/// (UQ_Employee_Tenant_Company_Code) - the in-memory tests assert nothing about it, by design.
/// </summary>
public sealed class EmployeeValidationTests
{
    // Email

    [Theory]
    [InlineData("maria.santos@example.com")]
    [InlineData("m.santos+payroll@example.co.uk")]
    public void EnsureValidEmail_WellFormed_Accepts(string email)
    {
        EmployeeValidator.EnsureValidEmail(email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidEmail_Missing_ThrowsEmailRequired(string? email)
    {
        var ex = Assert.Throws<HrValidationException>(
            () => EmployeeValidator.EnsureValidEmail(email));
        Assert.Equal(HrPayrollErrorCodes.EmployeeEmailRequired, ex.Code);
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("missing-at-sign.com")]
    [InlineData("a@b")]
    [InlineData("@example.com")]
    [InlineData("maria@.com")]
    [InlineData("maria santos@example.com")]
    public void EnsureValidEmail_Malformed_ThrowsInvalidEmail(string email)
    {
        var ex = Assert.Throws<HrValidationException>(
            () => EmployeeValidator.EnsureValidEmail(email));
        Assert.Equal(HrPayrollErrorCodes.InvalidEmployeeEmail, ex.Code);
    }

    // Bank details

    [Fact]
    public void EnsureValidBankDetails_ActiveBank_MissingBoth_ThrowsBankNameRequired()
    {
        var ex = Assert.Throws<HrValidationException>(
            () => EmployeeValidator.EnsureValidBankDetails(
                EmploymentStatus.Active, SalaryMode.Bank, null, null));
        Assert.Equal(HrPayrollErrorCodes.BankNameRequired, ex.Code);
    }

    [Fact]
    public void EnsureValidBankDetails_ActiveBank_MissingAccount_ThrowsBankAccountRequired()
    {
        var ex = Assert.Throws<HrValidationException>(
            () => EmployeeValidator.EnsureValidBankDetails(
                EmploymentStatus.Active, SalaryMode.Bank, "JPMorgan Chase", "  "));
        Assert.Equal(HrPayrollErrorCodes.BankAccountNumberRequired, ex.Code);
    }

    [Fact]
    public void EnsureValidBankDetails_ActiveBank_Complete_Accepts()
    {
        EmployeeValidator.EnsureValidBankDetails(
            EmploymentStatus.Active, SalaryMode.Bank, "JPMorgan Chase", "12345678");
    }

    [Theory]
    [InlineData(EmploymentStatus.Inactive)]
    [InlineData(EmploymentStatus.Suspended)]
    [InlineData(EmploymentStatus.Left)]
    public void EnsureValidBankDetails_NonActiveBankMode_SkipsGate(EmploymentStatus status)
    {
        // Departed staff are not paid: bank data is not required even for Bank mode.
        EmployeeValidator.EnsureValidBankDetails(status, SalaryMode.Bank, null, null);
    }

    [Fact]
    public void EnsureValidBankDetails_ActiveCashMode_SkipsGate()
    {
        // Cash/cheque staff carry no bank data.
        EmployeeValidator.EnsureValidBankDetails(EmploymentStatus.Active, SalaryMode.Cash, null, null);
        EmployeeValidator.EnsureValidBankDetails(EmploymentStatus.Active, SalaryMode.Cheque, null, null);
    }

    // Employment dates

    [Fact]
    public void EnsureValidEmploymentDates_RelievingBeforeJoining_Throws()
    {
        var ex = Assert.Throws<HrValidationException>(
            () => EmployeeValidator.EnsureValidEmploymentDates(
                new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 14)));
        Assert.Equal(HrPayrollErrorCodes.RelievingBeforeJoining, ex.Code);
    }

    [Fact]
    public void EnsureValidEmploymentDates_RelievingOnOrAfterJoining_Accepts()
    {
        EmployeeValidator.EnsureValidEmploymentDates(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15));
        EmployeeValidator.EnsureValidEmploymentDates(new DateOnly(2026, 1, 15), new DateOnly(2026, 6, 30));
        EmployeeValidator.EnsureValidEmploymentDates(new DateOnly(2026, 1, 15), null);
    }

    // Department tree

    private static Department Node(Guid id, Guid companyId, Guid? parentId = null) =>
        new()
        {
            Id = id,
            CompanyId = companyId,
            DepartmentName = "Node",
            ParentDepartmentId = parentId,
        };

    [Fact]
    public void EnsureValidParent_SelfParent_ThrowsParentIsSelf()
    {
        var id = Guid.NewGuid();
        var candidate = Node(id, Guid.NewGuid(), parentId: id);

        var ex = Assert.Throws<HrValidationException>(
            () => DepartmentValidator.EnsureValidParent(candidate, candidate));
        Assert.Equal(HrPayrollErrorCodes.ParentIsSelf, ex.Code);
    }

    [Fact]
    public void EnsureValidParent_ParentFromAnotherCompany_Throws()
    {
        var candidate = Node(Guid.NewGuid(), Guid.NewGuid(), parentId: Guid.NewGuid());
        var parent = Node(candidate.ParentDepartmentId!.Value, Guid.NewGuid());

        var ex = Assert.Throws<HrValidationException>(
            () => DepartmentValidator.EnsureValidParent(candidate, parent));
        Assert.Equal(HrPayrollErrorCodes.ParentNotInSameCompany, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_CandidateInsideItsOwnAncestorChain_ThrowsCycleDetected()
    {
        var candidateId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var grandparentId = Guid.NewGuid();

        var ex = Assert.Throws<HrValidationException>(
            () => DepartmentValidator.EnsureNoCycle(
                candidateId, new[] { parentId, grandparentId, candidateId }));
        Assert.Equal(HrPayrollErrorCodes.CycleDetected, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_LoopingStoredChain_ThrowsCycleDetected()
    {
        // Multi-level cycle: the stored chain of the proposed parent already loops
        // (parent -> grandparent -> parent) - grafting onto it must fail.
        var parentId = Guid.NewGuid();
        var grandparentId = Guid.NewGuid();

        var ex = Assert.Throws<HrValidationException>(
            () => DepartmentValidator.EnsureNoCycle(
                Guid.NewGuid(), new[] { parentId, grandparentId, parentId }));
        Assert.Equal(HrPayrollErrorCodes.CycleDetected, ex.Code);
    }

    [Fact]
    public void EnsureNoCycle_CleanChain_Accepts()
    {
        DepartmentValidator.EnsureNoCycle(
            Guid.NewGuid(), new[] { Guid.NewGuid(), Guid.NewGuid() });
    }
}
