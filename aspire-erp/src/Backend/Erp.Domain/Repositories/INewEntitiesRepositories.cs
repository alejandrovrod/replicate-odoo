using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
}

public interface IHrRepository
{
    Task AddAttendanceAsync(Attendance attendance, CancellationToken cancellationToken = default);
    Task AddLeaveApplicationAsync(LeaveApplication leaveApplication, CancellationToken cancellationToken = default);
}
