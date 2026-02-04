using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RX.Nyss.Data;
using RX.Nyss.Web.Features.NationalSocietyDashboard.Dto;
using RX.Nyss.Web.Features.Reports;
using RX.Nyss.Web.Services.ReportsDashboard;

namespace RX.Nyss.Web.Features.NationalSocietyDashboard
{
    public interface INationalSocietyDashboardSummaryService
    {
        Task<NationalSocietySummaryResponseDto> GetData(ReportsFilter filters);
    }

    public class NationalSocietyDashboardSummaryService : INationalSocietyDashboardSummaryService
    {
        private readonly IReportService _reportService;

        private readonly INyssContext _nyssContext;

        private readonly IReportsDashboardSummaryService _reportsDashboardSummaryService;

        public NationalSocietyDashboardSummaryService(
            IReportService reportService,
            INyssContext nyssContext,
            IReportsDashboardSummaryService reportsDashboardSummaryService)
        {
            _reportService = reportService;
            _nyssContext = nyssContext;
            _reportsDashboardSummaryService = reportsDashboardSummaryService;
        }

        public async Task<NationalSocietySummaryResponseDto> GetData(ReportsFilter filters)
        {
            if (!filters.NationalSocietyId.HasValue)
            {
                throw new InvalidOperationException("NationalSocietyId was not supplied");
            }

            var nationalSocietyId = filters.NationalSocietyId.Value;

            // Keep as IQueryable for methods that need it
            var dashboardReports = _reportService.GetDashboardHealthRiskEventReportsQuery(filters);
            var rawReportsWithDataCollectorAndActivityReports = _reportService.GetRawReportsWithDataCollectorAndActivityReportsQuery(filters);

            // Check if national society exists
            var nationalSocietyExists = await _nyssContext.NationalSocieties
                .AnyAsync(ns => ns.Id == nationalSocietyId);

            if (!nationalSocietyExists)
            {
                return null;
            }

            // Execute the queries that need to be materialized
            var dashboardReportsList = await dashboardReports.ToListAsync();
            var rawReportsList = await rawReportsWithDataCollectorAndActivityReports.ToListAsync();

            // Perform calculations
            return new NationalSocietySummaryResponseDto
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
