using Xunit;

// The integration suite shares ONE live SQL Server database and every WebApplicationFactory boots
// the full composition root (company/account/bank/role/user seeders). Running collections in
// parallel lets those seeders fight over the same rows, producing deadlocks and duplicate-key
// startup failures that have nothing to do with the behavior under test. Serialize the assembly:
// tests stay isolated through their own tenant/company rows, but startup writes never overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
