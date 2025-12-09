using System;
using System.Threading.Tasks;
using RX.Nyss.Common.Services;
using RX.Nyss.Common.Services.DhisClient;
using RX.Nyss.Common.Services.DhisClient.Dto;
using RX.Nyss.Common.Utils.Logging;
using RX.Nyss.Data.Repositories;
using RX.Nyss.ReportApi.Configuration;
using DhisReport = RX.Nyss.ReportApi.Features.Reports.Models.DhisReport;
using EidsrApiProperties = RX.Nyss.Web.Services.EidsrClient.Dto.EidsrApiProperties;

namespace RX.Nyss.ReportApi.Features.Reports.Handlers;

public interface IDhisReportHandler
{
    Task<bool> Handle(DhisReport dhisReport);
}

public class DhisReportHandler : IDhisReportHandler
{
    private readonly ILoggerAdapter _loggerAdapter;
    private readonly IDhisClient _dhisClient;
    private readonly INyssReportApiConfig _nyssReportApiConfig;
    private readonly ICryptographyService _cryptographyService;
    private readonly IDhisRepository _dhisRepository;

    public DhisReportHandler(
        ILoggerAdapter loggerAdapter,
        IDhisClient dhisClient,
        INyssReportApiConfig nyssReportApiConfig,
        ICryptographyService cryptographyService,
        IDhisRepository dhisRepository)
    {
        _loggerAdapter = loggerAdapter;
        _dhisClient = dhisClient;
        _nyssReportApiConfig = nyssReportApiConfig;
        _cryptographyService = cryptographyService;
        _dhisRepository = dhisRepository;
    }

    public async Task<bool> Handle(DhisReport dhisReport)
    {
        var isSuccess = true;
        try
        {
            if (dhisReport?.ReportId == null)
            {
                throw new ArgumentException($"DhisReport is null");
            }

            var report = _dhisRepository.GetReportsForDhis(dhisReport.ReportId.Value);
            var template = new DhisRegisterReportRequestTemplate
            {
                EidsrApiProperties = new EidsrApiProperties
                {
                    Url = report.DhisDbReportTemplate.EidsrApiProperties.Url,
                    UserName = report.DhisDbReportTemplate.EidsrApiProperties.UserName,
                    Password = _cryptographyService.Decrypt(
                        report.DhisDbReportTemplate.EidsrApiProperties.PasswordHash,
                        _nyssReportApiConfig.Key,
                            _nyssReportApiConfig.SupplementaryKey),
                },
                Program = report.DhisDbReportTemplate.Program,
                ReportLocationDataElementId = report.DhisDbReportTemplate.ReportLocationDataElementId,
                ReportGeoLocationDataElementId = report.DhisDbReportTemplate.ReportGeoLocationDataElementId,
                ReportHealthRiskDataElementId = report.DhisDbReportTemplate.ReportHealthRiskDataElementId,
                ReportSuspectedDiseaseDataElementId = report.DhisDbReportTemplate.ReportSuspectedDiseaseDataElementId,
                ReportStatusDataElementId = report.DhisDbReportTemplate.ReportStatusDataElementId,
                ReportGenderDataElementId = report.DhisDbReportTemplate.ReportGenderDataElementId,
                ReportAgeGroupDataElementId = report.DhisDbReportTemplate.ReportAgeGroupDataElementId,
                ReportCaseCountFemaleAgeAtLeastFiveDataElementId = report.DhisDbReportTemplate.ReportCaseCountFemaleAgeAtLeastFiveDataElementId,
                ReportCaseCountMaleAgeAtLeastFiveDataElementId = report.DhisDbReportTemplate.ReportCaseCountMaleAgeAtLeastFiveDataElementId,
                ReportCaseCountFemaleAgeBelowFiveDataElementId = report.DhisDbReportTemplate.ReportCaseCountFemaleAgeBelowFiveDataElementId,
                ReportCaseCountMaleAgeBelowFiveDataElementId = report.DhisDbReportTemplate.ReportCaseCountMaleAgeBelowFiveDataElementId,
                ReportDateDataElementId = report.DhisDbReportTemplate.ReportDateDataElementId,
                ReportTimeDataElementId = report.DhisDbReportTemplate.ReportTimeDataElementId,
                ReportDataCollectorIdDataElementId = report.DhisDbReportTemplate.ReportDataCollectorIdDataElementId
            };

            var data = new DhisRegisterReportRequestData
            {
                OrgUnit = report.DhisDbReportData.OrgUnit,
                EventDate = report.DhisDbReportData.EventDate,
                ReportLocation = report.DhisDbReportData.ReportLocation,
                ReportGeoLocation = report.DhisDbReportData.ReportGeoLocation,
                ReportHealthRisk = report.DhisDbReportData.ReportHealthRisk,
                ReportSuspectedDisease = report.DhisDbReportData.ReportSuspectedDisease,
                ReportStatus = report.DhisDbReportData.ReportStatus,
                ReportGender = report.DhisDbReportData.ReportGender,
                ReportAgeGroup = report.DhisDbReportData.ReportAgeGroup,
                ReportCaseCountFemaleAgeAtLeastFive = report.DhisDbReportData.ReportCaseCountFemaleAgeAtLeastFive,
                ReportCaseCountMaleAgeAtLeastFive = report.DhisDbReportData.ReportCaseCountMaleAgeAtLeastFive,
                ReportCaseCountFemaleAgeBelowFive = report.DhisDbReportData.ReportCaseCountFemaleAgeBelowFive,
                ReportCaseCountMaleAgeBelowFive = report.DhisDbReportData.ReportCaseCountMaleAgeBelowFive,
                ReportDate = report.DhisDbReportData.ReportDate,
                ReportTime = report.DhisDbReportData.ReportTime,
                ReportDataCollectorId = report.DhisDbReportData.ReportDataCollectorId
            };

            var request = DhisRegisterReportRequest.CreateDhisRegisterReportRequest(template, data);
            var result = await _dhisClient.RegisterReport(request);

            if (!result.IsSuccess)
            {
                isSuccess = false;
            }
        }
        catch (Exception e)
        {
            _loggerAdapter.Error(e.Message);
            isSuccess = false;
        }

        return isSuccess;
    }
}



