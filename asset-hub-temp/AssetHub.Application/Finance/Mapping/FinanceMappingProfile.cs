using AutoMapper;
using AssetHub.Application.Finance.Dtos;
using AssetHub.Domain.Finance;

namespace AssetHub.Application.Finance.Mapping;

public class FinanceMappingProfile : Profile
{
    public FinanceMappingProfile()
    {
        CreateMap<AssetFinanceBook, AssetFinanceBookDto>();

        CreateMap<AssetDepreciationSchedule, AssetDepreciationScheduleDto>();

        CreateMap<AssetDepreciationEntry, AssetDepreciationEntryDto>();

        CreateMap<AssetValueAdjustment, AssetValueAdjustmentDto>();

        CreateMap<AssetDisposal, AssetDisposalDto>();

        CreateMap<AssetCustodyTransfer, AssetCustodyTransferDto>()
            .ForMember(d => d.FromEmployeeName, opt => opt.MapFrom(s => s.FromEmployee != null ? $"{s.FromEmployee.FirstName} {s.FromEmployee.LastName}" : null))
            .ForMember(d => d.ToEmployeeName, opt => opt.MapFrom(s => $"{s.ToEmployee.FirstName} {s.ToEmployee.LastName}"))
            .ForMember(d => d.FromDepartmentName, opt => opt.Ignore())
            .ForMember(d => d.ToDepartmentName, opt => opt.Ignore());

        CreateMap<AssetRepairCapitalization, AssetRepairCapitalizationDto>();
    }
}