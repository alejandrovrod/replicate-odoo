using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll.Commands;

public sealed class CreateAttendanceCommandHandler : ICommandHandler<CreateAttendanceCommand, Result<AttendanceDto>>
{
    private readonly IHrRepository _hrRepository;

    public CreateAttendanceCommandHandler(IHrRepository hrRepository)
    {
        _hrRepository = hrRepository;
    }

    public async Task<Result<AttendanceDto>> HandleAsync(CreateAttendanceCommand request, CancellationToken cancellationToken = default)
    {
        var entity = new Attendance
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            EmployeeId = request.EmployeeId,
            AttendanceDate = request.AttendanceDate,
            Status = request.Status,
            Shift = request.Shift,
            InTime = request.InTime,
            OutTime = request.OutTime
        };

        await _hrRepository.AddAttendanceAsync(entity, cancellationToken);

        var dto = new AttendanceDto(
            entity.Id,
            entity.CompanyId,
            entity.EmployeeId,
            entity.AttendanceDate,
            entity.Status,
            entity.Shift,
            entity.InTime,
            entity.OutTime
        );

        return Result<AttendanceDto>.Success(dto);
    }
}

public sealed class CreateLeaveApplicationCommandHandler : ICommandHandler<CreateLeaveApplicationCommand, Result<LeaveApplicationDto>>
{
    private readonly IHrRepository _hrRepository;

    public CreateLeaveApplicationCommandHandler(IHrRepository hrRepository)
    {
        _hrRepository = hrRepository;
    }

    public async Task<Result<LeaveApplicationDto>> HandleAsync(CreateLeaveApplicationCommand request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<LeaveType>(request.LeaveType, out var parsedLeaveType))
        {
            return Result<LeaveApplicationDto>.Failure("HR-01", $"Invalid LeaveType: {request.LeaveType}");
        }

        var entity = new LeaveApplication
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            EmployeeId = request.EmployeeId,
            LeaveType = parsedLeaveType,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            TotalLeaveDays = request.TotalLeaveDays,
            Reason = request.Reason,
            Status = "Draft", // Initial status
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _hrRepository.AddLeaveApplicationAsync(entity, cancellationToken);

        var dto = new LeaveApplicationDto(
            entity.Id,
            entity.CompanyId,
            entity.EmployeeId,
            entity.LeaveType.ToString(),
            entity.FromDate,
            entity.ToDate,
            entity.TotalLeaveDays,
            entity.Status,
            entity.Reason,
            entity.CreatedAt
        );

        return Result<LeaveApplicationDto>.Success(dto);
    }
}
