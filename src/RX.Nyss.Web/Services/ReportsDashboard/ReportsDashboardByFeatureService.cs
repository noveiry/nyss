using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RX.Nyss.Common.Extensions;
using RX.Nyss.Common.Utils;
using RX.Nyss.Data.Concepts;
using RX.Nyss.Data.Models;
using RX.Nyss.Web.Features.Reports;
using RX.Nyss.Web.Services.ReportsDashboard.Dto;

namespace RX.Nyss.Web.Services.ReportsDashboard
{
    public interface IReportsDashboardByFeatureService
    {
        Task<IList<ReportByFeaturesAndDateResponseDto>> GetReportsGroupedByFeaturesAndDate(ReportsFilter filters, DatesGroupingType groupingType, DayOfWeek epiWeekStartDay);
    }

    public class ReportsDashboardByFeatureService : IReportsDashboardByFeatureService
    {
        private readonly IReportService _reportService;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ReportsDashboardByFeatureService(
            IReportService reportService,
            IDateTimeProvider dateTimeProvider)
        {
            _reportService = reportService;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IList<ReportByFeaturesAndDateResponseDto>> GetReportsGroupedByFeaturesAndDate(ReportsFilter filters, DatesGroupingType groupingType, DayOfWeek epiWeekStartDay)
        {
            var reports = _reportService.GetDashboardHealthRiskEventReportsQuery(filters);


            var humanReports = reports
                .Where(r => r.ProjectHealthRisk.HealthRisk.HealthRiskType == HealthRiskType.Human);

            return groupingType switch
            {
                DatesGroupingType.Day =>
                await GroupReportsByFeaturesAndDay(humanReports, filters.StartDate.DateTime.AddHours(filters.UtcOffset), filters.EndDate.DateTime.AddHours(filters.UtcOffset), filters.UtcOffset),

                DatesGroupingType.Week =>
                await GroupReportsByFeaturesAndWeek(humanReports, filters.StartDate.DateTime.AddHours(filters.UtcOffset), filters.EndDate.DateTime.AddHours(filters.UtcOffset), epiWeekStartDay),

                _ =>
                throw new InvalidOperationException()
            };
        }

        private static async Task<IList<ReportByFeaturesAndDateResponseDto>> GroupReportsByFeaturesAndDay(IQueryable<Report> reports, DateTime startDate, DateTime endDate, int utcOffset)
        {
            var groupedReports = await reports
                .GroupBy(r => r.ReceivedAt.AddHours(utcOffset).Date)
                .Select(grouping => new
                {
                    Period = grouping.Key,
                    CountFemalesAtLeastFive = grouping.Sum(g => g.ReportedCase.CountFemalesAtLeastFive ?? 0),
                    CountFemalesBelowFive = grouping.Sum(g => g.ReportedCase.CountFemalesBelowFive ?? 0),
                    CountMalesAtLeastFive = grouping.Sum(g => g.ReportedCase.CountMalesAtLeastFive ?? 0),
                    CountMalesBelowFive = grouping.Sum(g => g.ReportedCase.CountMalesBelowFive ?? 0),
                    CountUnspecifiedSexAndAge = grouping.Sum(g => g.ReportedCase.CountUnspecifiedSexAndAge ?? 0)
                })
                .ToListAsync();

            var missingDays = startDate.GetDaysRange(endDate)
                .Where(day => !groupedReports.Any(r => r.Period == day.Date))
                .Select(day => new
                {
                    Period = day,
                    CountFemalesAtLeastFive = 0,
                    CountFemalesBelowFive = 0,
                    CountMalesAtLeastFive = 0,
                    CountMalesBelowFive = 0,
                    CountUnspecifiedSexAndAge = 0
                });

            return groupedReports
                .Union(missingDays)
                .OrderBy(r => r.Period)
                .Select(x => new ReportByFeaturesAndDateResponseDto
                {
                    Period = x.Period.ToString("dd/MM/yy", CultureInfo.InvariantCulture),
                    CountFemalesAtLeastFive = x.CountFemalesAtLeastFive,
                    CountFemalesBelowFive = x.CountFemalesBelowFive,
                    CountMalesAtLeastFive = x.CountMalesAtLeastFive,
                    CountMalesBelowFive = x.CountMalesBelowFive,
                    CountUnspecifiedSexAndAge = x.CountUnspecifiedSexAndAge
                })
                .ToList();
        }

        private async Task<IList<ReportByFeaturesAndDateResponseDto>> GroupReportsByFeaturesAndWeek(IQueryable<Report> reports, DateTime startDate, DateTime endDate, DayOfWeek epiWeekStartDay)
        {
            var groupedReports = await reports
                .GroupBy(r => new
                {
                    r.EpiYear,
                    r.EpiWeek
                })
                .Select(grouping => new
                {
                    EpiPeriod = grouping.Key,
                    CountFemalesAtLeastFive = grouping.Sum(g => g.ReportedCase.CountFemalesAtLeastFive ?? 0),
                    CountFemalesBelowFive = grouping.Sum(g => g.ReportedCase.CountFemalesBelowFive ?? 0),
                    CountMalesAtLeastFive = grouping.Sum(g => g.ReportedCase.CountMalesAtLeastFive ?? 0),
                    CountMalesBelowFive = grouping.Sum(g => g.ReportedCase.CountMalesBelowFive ?? 0),
                    CountUnspecifiedSexAndAge = grouping.Sum(g => g.ReportedCase.CountUnspecifiedSexAndAge ?? 0)
                })
                .ToListAsync();

            var missingWeeks = _dateTimeProvider.GetEpiDateRange(startDate, endDate, epiWeekStartDay)
                .Where(epiDate => !groupedReports.Any(r => r.EpiPeriod.EpiYear == epiDate.EpiYear && r.EpiPeriod.EpiWeek == epiDate.EpiWeek))
                .Select(epiDate => new
                {
                    EpiPeriod = new
                    {
                        epiDate.EpiYear,
                        epiDate.EpiWeek
                    },
                    CountFemalesAtLeastFive = 0,
                    CountFemalesBelowFive = 0,
                    CountMalesAtLeastFive = 0,
                    CountMalesBelowFive = 0,
                    CountUnspecifiedSexAndAge = 0
                });

            return groupedReports
                .Union(missingWeeks)
                .OrderBy(r => r.EpiPeriod.EpiYear)
                .ThenBy(r => r.EpiPeriod.EpiWeek)
                .Select(x => new ReportByFeaturesAndDateResponseDto
                {
                    Period = $"{x.EpiPeriod.EpiYear}/{x.EpiPeriod.EpiWeek}",
                    CountFemalesAtLeastFive = x.CountFemalesAtLeastFive,
                    CountFemalesBelowFive = x.CountFemalesBelowFive,
                    CountMalesAtLeastFive = x.CountMalesAtLeastFive,
                    CountMalesBelowFive = x.CountMalesBelowFive,
                    CountUnspecifiedSexAndAge = x.CountUnspecifiedSexAndAge
                })
                .ToList();
        }
    }
}
