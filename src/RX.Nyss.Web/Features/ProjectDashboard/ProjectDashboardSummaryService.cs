using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RX.Nyss.Data;
using RX.Nyss.Web.Features.ProjectDashboard.Dto;
using RX.Nyss.Web.Features.Reports;
using RX.Nyss.Web.Services.ReportsDashboard;

namespace RX.Nyss.Web.Features.ProjectDashboard
{
    public interface IProjectDashboardSummaryService
    {
        Task<ProjectSummaryResponseDto> GetData(ReportsFilter filters);
    }

    public class ProjectDashboardSummaryService : IProjectDashboardSummaryService
    {
        private readonly IReportService _reportService;

        private readonly INyssContext _nyssContext;

        private readonly IReportsDashboardSummaryService _reportsDashboardSummaryService;

        public ProjectDashboardSummaryService(
            IReportService reportService,
            INyssContext nyssContext,
            IReportsDashboardSummaryService reportsDashboardSummaryService)
        {
            _reportService = reportService;
            _nyssContext = nyssContext;
            _reportsDashboardSummaryService = reportsDashboardSummaryService;
        }

        /*public async Task<ProjectSummaryResponseDto> GetData(ReportsFilter filters)
        {
            if (!filters.ProjectId.HasValue)
            {
                throw new InvalidOperationException("ProjectId was not supplied");
            }

            var dashboardReports = _reportService.GetDashboardHealthRiskEventReportsQuery(filters);
            var rawReportsWithDataCollectorAndActivityReports = _reportService.GetRawReportsWithDataCollectorAndActivityReportsQuery(filters);

            return await _nyssContext.Projects
                .AsNoTracking()
                .Where(p => p.Id == filters.ProjectId.Value)
                .Select(p => new
                {
                    ActiveDataCollectorCount = rawReportsWithDataCollectorAndActivityReports.Select(r => r.DataCollector.Id).Distinct().Count()
                })
                .Select(data => new ProjectSummaryResponseDto
                {
                    TotalReportCount = dashboardReports.Sum(r => r.ReportedCaseCount),
                    ActiveDataCollectorCount = data.ActiveDataCollectorCount,
                    DataCollectionPointSummary = _reportsDashboardSummaryService.DataCollectionPointsSummary(dashboardReports),
                    AlertsSummary = _reportsDashboardSummaryService.AlertsSummary(filters),
                    NumberOfDistricts = rawReportsWithDataCollectorAndActivityReports.Select(r => r.Village.District).Distinct().Count(),
                    NumberOfVillages = rawReportsWithDataCollectorAndActivityReports.Select(r => r.Village).Distinct().Count()
                })
                .FirstOrDefaultAsync();
        }*/

        public async Task<ProjectSummaryResponseDto> GetData(ReportsFilter filters)
        {
            if (!filters.ProjectId.HasValue)
            {
                throw new InvalidOperationException("ProjectId was not supplied");
            }

            var projectId = filters.ProjectId.Value;

            // Check if project exists
            var projectExists = await _nyssContext.Projects
                .AsNoTracking()
                .AnyAsync(p => p.Id == projectId);

            if (!projectExists)
            {
                return null;
            }

            // Keep as IQueryable for methods that need it
            var dashboardReports = _reportService.GetDashboardHealthRiskEventReportsQuery(filters);
            var rawReportsWithDataCollectorAndActivityReports = _reportService.GetRawReportsWithDataCollectorAndActivityReportsQuery(filters);

            // Execute the queries that need to be materialized
            var dashboardReportsList = await dashboardReports.ToListAsync();
            var rawReportsList = await rawReportsWithDataCollectorAndActivityReports.ToListAsync();

            // Perform calculations in memory
            return new ProjectSummaryResponseDto
            {
                TotalReportCount = dashboardReportsList.Sum(r => r.ReportedCaseCount),
                ActiveDataCollectorCount = rawReportsList
                    .Where(r => r.DataCollector != null)
                    .Select(r => r.DataCollector.Id)
                    .Distinct()
                    .Count(),
                DataCollectionPointSummary = _reportsDashboardSummaryService.DataCollectionPointsSummary(dashboardReports),
                AlertsSummary = _reportsDashboardSummaryService.AlertsSummary(filters),
                NumberOfDistricts = rawReportsList
                    .Where(r => r.Village?.District != null)
                    .Select(r => r.Village.District)
                    .Distinct()
                    .Count(),
                NumberOfVillages = rawReportsList
                    .Where(r => r.Village != null)
                    .Select(r => r.Village)
                    .Distinct()
                    .Count()
            };
        }
    }
}
