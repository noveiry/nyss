using System;
using System.Collections.Generic;
using System.Linq;
using RX.Nyss.Common.Utils.Logging;
using RX.Nyss.Data.Models;

namespace RX.Nyss.Data.Repositories;

public interface IReportsConverter
{
    List<EidsrDbReportData> ConvertReports(List<RawReport> reports, DateTime alertDate, int englishContentLanguageId);
    DhisDbReportData ConvertDhisReport(RawReport rawReport, DateTime reportDate, int englishContentLanguageId);
}

public class ReportsConverter : IReportsConverter
{
    private readonly ILoggerAdapter _logger;

    public ReportsConverter(ILoggerAdapter logger)
    {
        _logger = logger;
    }
    public List<EidsrDbReportData> ConvertReports(List<RawReport> reports, DateTime alertDate, int englishContentLanguageId)
    {
        var eidsrDbReportData = new List<EidsrDbReportData>();

        // format for Eidsr purposes data of the reports
        foreach (var report in reports)
        {
            try
            {
                eidsrDbReportData.Add(ConvertSingleReport(report, alertDate, englishContentLanguageId));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error during converting report to EIDSR report, skipping malformed report.");
            }
        }

        // generate aggregated reports - group them by the organisation unit associated with RawReport District
        return SquashReports(eidsrDbReportData);
    }

    private EidsrDbReportData ConvertSingleReport(RawReport rawReport, DateTime alertDate, int englishContentLanguageId)
    {
        if (rawReport.Village.District.EidsrOrganisationUnits.OrganisationUnitId == null)
        {
            throw new ArgumentException("Report's location has no organisation unit.");
        }

        return new EidsrDbReportData
        {
            OrgUnit = rawReport.Village.District.EidsrOrganisationUnits.OrganisationUnitId,
            EventDate = alertDate.ToString("yyyy-MM-dd"),
            Location = $"{rawReport?.Village?.District?.Region?.Name}/{rawReport?.Village?.District?.Name}/{rawReport?.Village?.Name}",
            DateOfOnset = rawReport?.Report?.ReceivedAt.ToString("u"),
            PhoneNumber = rawReport?.Report?.PhoneNumber,
            SuspectedDisease = ExtractSuspectedDiseases(englishContentLanguageId, rawReport?.Report),
            EventType = ExtractHealthRisk(englishContentLanguageId,rawReport?.Report),
            //EventType = rawReport?.Report?.ProjectHealthRisk?.HealthRisk?.HealthRiskType.ToString(),
            Gender = ExtractGender(report: rawReport?.Report)
        };
    }

    private string ExtractGender(Report report)
    {
        if (report?.ReportedCase != null && (report.ReportedCase.CountFemalesBelowFive == 1 || report.ReportedCase.CountFemalesAtLeastFive == 1))
        {
            return "female";
        }

        if (report?.ReportedCase != null && (report.ReportedCase.CountMalesBelowFive == 1 || report.ReportedCase.CountMalesAtLeastFive == 1))
        {
            return "male";
        }

        if (report?.ReportedCase != null && (report.ReportedCase.CountUnspecifiedSexAndAge == 1))
        {
            return "Unspecified Sex and Age";
        }

        return "";
    }

    private string ExtractGender(RawReport rawReport)
    {
        if (rawReport.Report?.ReportedCase != null && (rawReport.Report.ReportedCase.CountFemalesBelowFive == 1 || rawReport.Report.ReportedCase.CountFemalesAtLeastFive == 1))
        {
            return "female";
        }

        if (rawReport.Report?.ReportedCase != null && (rawReport.Report.ReportedCase.CountMalesBelowFive == 1 || rawReport.Report.ReportedCase.CountMalesAtLeastFive == 1))
        {
            return "male";
        }

        if (rawReport.Report?.ReportedCase != null && (rawReport.Report.ReportedCase.CountUnspecifiedSexAndAge == 1))
        {
            return "Unspecified Sex and Age";
        }

        return "";
    }

    private static string ExtractSuspectedDiseases(int englishContentLanguageId, Report report)
    {
        try
        {
            var suspectedDiseasesLanguageContents = report
                .ProjectHealthRisk
                .HealthRisk
                .HealthRiskSuspectedDiseases
                .SelectMany(s => s.SuspectedDisease.LanguageContents
                    .Where(c => c.ContentLanguageId == englishContentLanguageId))
                .Select(x => x.Name);

            var suspectedDiseases = string.Join('/', suspectedDiseasesLanguageContents);
            return suspectedDiseases;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private static string ExtractHealthRisk(int englishContentLanguageId, Report report)
    {
        try
        {
            var healthRiskLanguageContent = report
                .ProjectHealthRisk
                .HealthRisk
                .LanguageContents
                .SelectMany(s => s.HealthRisk.LanguageContents 
                    .Where(c => c.ContentLanguageId == englishContentLanguageId))
                .Select(x => x.Name);

            return healthRiskLanguageContent.First();
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private List<EidsrDbReportData> SquashReports(List<EidsrDbReportData> reports)
    {
        // for each reports group (grouped by org unit) create one EidsrDbReportData
        // (EventDate should be the same for all alerts, but we aggregate by that for safety)
        return reports.GroupBy(x => new
        {
            x.OrgUnit,
            x.EventDate
        })
            .Select(orgUnitGroup =>
            {
                return new EidsrDbReportData
                {
                    OrgUnit = orgUnitGroup.Key.OrgUnit,
                    EventDate = orgUnitGroup.Key.EventDate,
                    Gender = CreateValuesAndCountsString(orgUnitGroup.Select(x => x.Gender).ToList()),
                    Location = CreateValuesAndString(orgUnitGroup.Select(x => x.Location).ToList()),
                    EventType = CreateValuesAndString(orgUnitGroup.Select(x => x.EventType).ToList()),
                    PhoneNumber = CreateValuesAndCountsString(orgUnitGroup.Select(x => x.PhoneNumber).ToList()),
                    SuspectedDisease = CreateValuesAndString(orgUnitGroup.Select(x => x.SuspectedDisease).ToList()),
                    DateOfOnset = CreateValuesAndCountsString(orgUnitGroup.Select(x => x.DateOfOnset).ToList())
                };
            })
            .ToList();
    }

    private string CreateValuesAndCountsString(List<string> list)
    {
        var valueCountPairs = from x in list
                              group x by x into g
                              let count = g.Count()
                              orderby count descending
                              select new { Value = g.Key, Count = count };

        return string.Join(", ", valueCountPairs
            .Where(valueCountPair => !string.IsNullOrEmpty(valueCountPair.Value))
            .Select(valueCountPair =>
                valueCountPair.Count == 1
                    ? $"{valueCountPair.Value}"
                    : $"{valueCountPair.Value} ({valueCountPair.Count})"));
    }

    private string CreateValuesAndString(List<string> list)
    {
        var valuePairs = from x in list
                         group x by x into g
                         select new { Value = g.Key };

        return string.Join(", ", valuePairs
            .Where(vp => !string.IsNullOrEmpty(vp.Value))
            .Select(vp => $"{vp.Value}"));
    }

    public DhisDbReportData ConvertDhisReport(RawReport rawReport, DateTime reportDate, int englishContentLanguageId)
    {
        if (rawReport.Village.District.EidsrOrganisationUnits.OrganisationUnitId == null)
        {
            throw new ArgumentException("Report's location has no organisation unit.");
        }

        return new DhisDbReportData
        {
            OrgUnit = rawReport.Village.District.EidsrOrganisationUnits.OrganisationUnitId,
            EventDate = reportDate.ToString("yyyy-MM-dd"),

            ReportLocation = $"{rawReport?.Village?.District?.Region?.Name}/{rawReport?.Village?.District?.Name}/{rawReport?.Village?.Name}",
            ReportGeoLocation = rawReport.Report.Location,
            ReportSuspectedDisease = ExtractSuspectedDiseases(englishContentLanguageId, rawReport?.Report),
            ReportHealthRisk = ExtractHealthRisk(englishContentLanguageId, rawReport?.Report),
            ReportStatus = rawReport?.Report?.Status.ToString(),
            ReportGender = rawReport?.Report?.ReportedCase.CountFemalesAtLeastFive > 0 || rawReport?.Report?.ReportedCase.CountFemalesBelowFive > 0
                ? "Female"
                : "Male",
            ReportAgeGroup = rawReport?.Report?.ReportedCase.CountFemalesAtLeastFive > 0 || rawReport?.Report?.ReportedCase.CountMalesAtLeastFive > 0
                ? ">5 years"
                : "0-4 years",
            ReportCaseCountFemaleAgeAtLeastFive = rawReport?.Report?.ReportedCase?.CountFemalesAtLeastFive ?? 0,
            ReportCaseCountMaleAgeAtLeastFive = rawReport?.Report?.ReportedCase?.CountMalesAtLeastFive ?? 0,
            ReportCaseCountFemaleAgeBelowFive = rawReport?.Report?.ReportedCase?.CountFemalesBelowFive ?? 0,
            ReportCaseCountMaleAgeBelowFive = rawReport?.Report?.ReportedCase?.CountMalesBelowFive ?? 0,
            ReportDate = rawReport?.Report?.ReceivedAt.Date.ToString("yyyy-MM-dd") ?? DateTime.Now.Date.ToString("yyyy-MM-dd"),
            ReportTime = rawReport?.Report?.ReceivedAt.ToString("HH:mm:ss") ?? DateTime.Now.ToString("HH:mm:ss"),
            ReportDataCollectorId = rawReport?.Report?.DataCollector?.Id ?? 0
        };
    }
}
