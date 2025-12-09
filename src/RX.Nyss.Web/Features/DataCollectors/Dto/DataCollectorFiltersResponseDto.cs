using System.Collections.Generic;
using RX.Nyss.Web.Features.NationalSocietyStructure.Dto;

namespace RX.Nyss.Web.Features.DataCollectors.Dto
{
    public class DataCollectorFiltersReponseDto
    {
        public IEnumerable<DataCollectorSupervisorResponseDto> Supervisors { get; set; }
        public StructureResponseDto Locations { get; set; }
    }
}