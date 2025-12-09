using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RX.Nyss.Common.Services.StringsResources;
using RX.Nyss.Common.Utils;
using RX.Nyss.Common.Utils.DataContract;
using RX.Nyss.Common.Utils.Logging;
using RX.Nyss.Data;
using RX.Nyss.Data.Concepts;
using RX.Nyss.Data.Models;
using RX.Nyss.Data.Queries;
using RX.Nyss.Web.Configuration;
using RX.Nyss.Web.Features.Common.Dto;
using RX.Nyss.Web.Features.Common.Extensions;
using RX.Nyss.Web.Features.NationalSocietyStructure;
using RX.Nyss.Web.Features.Projects;
using RX.Nyss.Web.Features.Reports.Dto;
using RX.Nyss.Web.Features.Users;
using RX.Nyss.Web.Services;
using RX.Nyss.Web.Services.Authorization;
using RX.Nyss.Web.Services.EidsrService;
using RX.Nyss.Web.Utils.DataContract;
using RX.Nyss.Web.Utils.Extensions;
using static RX.Nyss.Common.Utils.DataContract.Result;

namespace RX.Nyss.Web.Features.Reports;

public interface IReportService
{
    Task<Result<PaginatedList<ReportListResponseDto>>> List(int projectId, int pageNumber, ReportListFilterRequestDto filter);

    Task<Result<ReportListFilterResponseDto>> GetFilters(int nationalSocietyId);

    Task<Result<HumanHealthRiskResponseDto>> GetHumanHealthRisksForProject(int projectId);

    IQueryable<RawReport> GetRawReportsWithDataCollectorQuery(ReportsFilter filters);

    IQueryable<RawReport> GetRawReportsWithDataCollectorAndActivityReportsQuery(ReportsFilter filters);

    IQueryable<Report> GetDashboardHealthRiskEventReportsQuery(ReportsFilter filters);

    Task<Result> AcceptReport(int reportId);

    Task<Result> DismissReport(int reportId);
}

public class ReportService : IReportService
{
    private readonly INyssWebConfig _config;

    private readonly INyssContext _nyssContext;

    private readonly IUserService _userService;

    private readonly IProjectService _projectService;

    private readonly IAuthorizationService _authorizationService;

    private readonly IDateTimeProvider _dateTimeProvider;

    private readonly IStringsService _stringsService;

    private readonly INationalSocietyStructureService _nationalSocietyStructureService;

    private readonly IEidsrService _dhisService;

    private readonly ILoggerAdapter _loggerAdapter;

    public ReportService(
        INyssContext nyssContext,
        IUserService userService,
        IProjectService projectService,
        INyssWebConfig config,
        IAuthorizationService authorizationService,
        IDateTimeProvider dateTimeProvider,
        IStringsService stringsService,
        IEidsrService dhisService,
        ILoggerAdapter loggerAdapter,
        INationalSocietyStructureService nationalSocietyStructureService)
    {
        _nyssContext = nyssContext;
        _userService = userService;
        _projectService = projectService;
        _config = config;
        _authorizationService = authorizationService;
        _dateTimeProvider = dateTimeProvider;
        _stringsService = stringsService;
        _nationalSocietyStructureService = nationalSocietyStructureService;
        _dhisService = dhisService;
        _loggerAdapter = loggerAdapter;
    }

    public async Task<Result<PaginatedList<ReportListResponseDto>>> List(int projectId, int pageNumber, ReportListFilterRequestDto filter)
{
    var currentUserName = _authorizationService.GetCurrentUserName();
    var currentRole = (await _authorizationService.GetCurrentUser()).Role;

    var isSupervisor = currentRole == Role.Supervisor;
    var isHeadSupervisor = currentRole == Role.HeadSupervisor;
    var currentUserId = await _nyssContext.Users.FilterAvailable()
        .Where(u => u.EmailAddress == currentUserName)
        .Select(u => u.Id)
        .SingleOrDefaultAsync();

    if (currentUserId == default)
    {
        return Success(new PaginatedList<ReportListResponseDto>(new List<ReportListResponseDto>(), 0, pageNumber, _config.PaginationRowsPerPage));
    }

    var userApplicationLanguageCode = await _userService.GetUserApplicationLanguageCode(currentUserName);
    var strings = await _stringsService.GetForCurrentUser();

    var baseQuery = await BuildRawReportsBaseQuery(filter, projectId);

    if (baseQuery == null)
    {
        return Success(new PaginatedList<ReportListResponseDto>(new List<ReportListResponseDto>(), 0, pageNumber, _config.PaginationRowsPerPage));
    }

    var currentUserOrganization = await _nyssContext.Projects
        .Where(p => p.Id == projectId)
        .SelectMany(p => p.NationalSociety.NationalSocietyUsers)
        .Where(uns => uns.User.Id == currentUserId)
        .Select(uns => uns.Organization)
        .SingleOrDefaultAsync();

    var currentUserOrganizationId = currentUserOrganization?.Id ?? 0;
    var supervisorId = 0;
    var headSupervisorId = 0;

    var result = baseQuery.Select(r => new ReportListResponseDto
    {
        Id = r.Id,
        IsAnonymized = currentRole != Role.Administrator && !r.NationalSociety.NationalSocietyUsers.Any(
                nsu => (nsu.UserId == r.DataCollector.Supervisor.Id && nsu.OrganizationId == currentUserOrganizationId)
                    || (nsu.UserId == r.DataCollector.HeadSupervisor.Id && nsu.OrganizationId == currentUserOrganizationId)),
        OrganizationName = r.NationalSociety.NationalSocietyUsers
                .Where(nsu => nsu.UserId == (r.DataCollector != null ? r.DataCollector.Supervisor.Id : 0) || nsu.UserId == (r.DataCollector != null ? r.DataCollector.HeadSupervisor.Id : 0))
                .Select(nsu => nsu.Organization.Name)
                .FirstOrDefault(),
        DateTime = r.ReceivedAt.AddHours(filter != null ? filter.UtcOffset : 0),
        HealthRiskName = r.Report != null && r.Report.ProjectHealthRisk != null && r.Report.ProjectHealthRisk.HealthRisk != null
                ? r.Report.ProjectHealthRisk.HealthRisk.LanguageContents
                    .Where(lc => lc.ContentLanguage.LanguageCode == userApplicationLanguageCode)
                    .Select(lc => lc.Name)
                    .SingleOrDefault()
                : null,
        IsActivityReport = (r.Report != null && r.Report.ProjectHealthRisk != null && r.Report.ProjectHealthRisk.HealthRisk != null && r.Report.ProjectHealthRisk.HealthRisk.HealthRiskCode == 99)
                || (r.Report != null && r.Report.ProjectHealthRisk != null && r.Report.ProjectHealthRisk.HealthRisk != null && r.Report.ProjectHealthRisk.HealthRisk.HealthRiskCode == 98),
        IsValid = r.Report != null,
        Region = r.Village != null && r.Village.District != null && r.Village.District.Region != null ? r.Village.District.Region.Name : null,
        District = r.Village != null && r.Village.District != null ? r.Village.District.Name : null,
        Village = r.Village != null ? r.Village.Name : null,
        Zone = r.Zone != null ? r.Zone.Name : null,
        DataCollectorDisplayName = r.DataCollector != null && r.DataCollector.DataCollectorType == DataCollectorType.CollectionPoint
                ? r.DataCollector.Name
                : (r.DataCollector != null ? r.DataCollector.DisplayName : null),
        SupervisorName = r.DataCollector != null ? (r.DataCollector.Supervisor != null ? r.DataCollector.Supervisor.Name : (r.DataCollector.HeadSupervisor != null ? r.DataCollector.HeadSupervisor.Name : null)) : null,
        PhoneNumber = r.Sender,
        Alert = r.Report.ReportAlerts
                .OrderByDescending(ra => ra.AlertId)
                .Select(ra => new ReportListAlert
                {
                    Id = ra.AlertId,
                    Status = ra.Alert != null ? ra.Alert.Status : default,
                    ReportWasCrossCheckedBeforeEscalation = (ra.Report != null && ra.Alert != null) &&
                        (
                            (ra.Report.AcceptedAt.HasValue && ra.Alert.EscalatedAt.HasValue && ra.Report.AcceptedAt < ra.Alert.EscalatedAt)
                            || (ra.Report.RejectedAt.HasValue && ra.Alert.EscalatedAt.HasValue && ra.Report.RejectedAt < ra.Alert.EscalatedAt)
                        )
                })
                .FirstOrDefault(),
        ReportId = r.ReportId,
        ReportType = r.Report != null ? (ReportType?)r.Report.ReportType : null,
        Message = r.Text,
        CountMalesBelowFive = (r.Report != null && r.Report.ReportedCase != null) ? r.Report.ReportedCase.CountMalesBelowFive : (int?)null,
        CountMalesAtLeastFive = (r.Report != null && r.Report.ReportedCase != null) ? r.Report.ReportedCase.CountMalesAtLeastFive : (int?)null,
        CountFemalesBelowFive = (r.Report != null && r.Report.ReportedCase != null) ? r.Report.ReportedCase.CountFemalesBelowFive : (int?)null,
        CountFemalesAtLeastFive = (r.Report != null && r.Report.ReportedCase != null) ? r.Report.ReportedCase.CountFemalesAtLeastFive : (int?)null,
        ReferredCount = (r.Report != null && r.Report.DataCollectionPointCase != null) ? r.Report.DataCollectionPointCase.ReferredCount : (int?)null,
        Status = r.Report != null ? r.Report.Status : ReportStatus.New,
        ReportErrorType = r.ErrorType,
        DataCollectorIsDeleted = r.DataCollector != null && r.DataCollector.Name == Anonymization.Text,
        IsCorrected = r.MarkedAsCorrectedAtUtc != null,
    })
        //ToDo: order base on filter.OrderBy property
        .OrderBy(r => r.DateTime, filter?.SortAscending ?? true);

    var rowsPerPage = _config.PaginationRowsPerPage;
    var reports = await result
        .Page(pageNumber, rowsPerPage)
        .ToListAsync<IReportListResponseDto>();

    if (reports == null)
    {
        reports = new List<IReportListResponseDto>();
    }

    if (filter?.DataCollectorType != ReportListDataCollectorType.UnknownSender)
    {
        AnonymizeCrossOrganizationReports(reports, currentUserOrganization?.Name, strings);
    }

    var totalCount = await baseQuery.CountAsync();
    return Success(reports.Cast<ReportListResponseDto>().AsPaginatedList(pageNumber, totalCount, rowsPerPage));
}



    public async Task<Result<ReportListFilterResponseDto>> GetFilters(int projectId)
    {
        var healthRiskTypes = new List<HealthRiskType>
        {
            HealthRiskType.Human,
            HealthRiskType.NonHuman,
            HealthRiskType.UnusualEvent,
            HealthRiskType.Activity
        };
        var projectHealthRiskNames = await _projectService.GetHealthRiskNames(projectId, healthRiskTypes);
        var nationalSocietyId = await _nyssContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.NationalSocietyId)
            .SingleAsync();
        var locations = await _nationalSocietyStructureService.Get(nationalSocietyId);

        var dto = new ReportListFilterResponseDto
        {
            HealthRisks = projectHealthRiskNames,
            Locations = locations
        };

        return Success(dto);
    }

    public async Task<Result<HumanHealthRiskResponseDto>> GetHumanHealthRisksForProject(int projectId)
    {
        var humanHealthRiskType = new List<HealthRiskType> { HealthRiskType.Human };
        var projectHealthRisks = await _projectService.GetHealthRiskNames(projectId, humanHealthRiskType);

        var dto = new HumanHealthRiskResponseDto { HealthRisks = projectHealthRisks };

        return Success(dto);
    }

    public IQueryable<RawReport> GetRawReportsWithDataCollectorQuery(ReportsFilter filters) =>
        _nyssContext.RawReports
            .AsNoTracking()
            .FilterByReportStatus(filters.ReportStatus)
            .FromKnownDataCollector()
            .FilterByArea(filters.Area)
            .FilterByDataCollectorType(filters.DataCollectorType)
            .FilterByOrganization(filters.OrganizationId)
            .FilterByProject(filters.ProjectId)
            .FilterReportsByNationalSociety(filters.NationalSocietyId)
            .FilterByDate(filters.StartDate, filters.EndDate)
            .FilterByHealthRisks(filters.HealthRisks)
            .FilterByTrainingMode(filters.TrainingStatus);

    public IQueryable<RawReport> GetRawReportsWithDataCollectorAndActivityReportsQuery(ReportsFilter filters) =>
        _nyssContext.RawReports
            .AsNoTracking()
            .FilterByReportStatus(filters.ReportStatus)
            .FromKnownDataCollector()
            .FilterByArea(filters.Area)
            .FilterByDataCollectorType(filters.DataCollectorType)
            .FilterByOrganization(filters.OrganizationId)
            .FilterByProject(filters.ProjectId)
            .FilterReportsByNationalSociety(filters.NationalSocietyId)
            .FilterByDate(filters.StartDate, filters.EndDate)
            .FilterByHealthRisksWithActivityReports(filters.HealthRisks)
            .FilterByTrainingMode(filters.TrainingStatus);


    public IQueryable<Report> GetDashboardHealthRiskEventReportsQuery(ReportsFilter filters) =>
        GetRawReportsWithDataCollectorQuery(filters)
            .AllSuccessfulReports()
            .Select(r => r.Report)
            .Where(r => r.ProjectHealthRisk.HealthRisk.HealthRiskType != HealthRiskType.Activity);

    public async Task<Result> AcceptReport(int reportId)
    {
        var currentUser = await _authorizationService.GetCurrentUser();
        var report = await _nyssContext.RawReports
            .Where(r => r.Id == reportId && r.Report != null)
            .Select(r => r.Report)
            .FirstOrDefaultAsync();

        if (report == null)
        {
            return Error(ResultKey.Report.ReportNotFound);
        }

        if (report.Status == ReportStatus.Accepted)
        {
            return Error(ResultKey.Report.AlreadyCrossChecked);
        }

        if (report.ReportType == ReportType.DataCollectionPoint)
        {
            return Error(ResultKey.Report.CannotCrossCheckDcpReport);
        }

        if (report.Location == null)
        {
            return Error(ResultKey.Report.CannotCrossCheckReportWithoutLocation);
        }

        report.AcceptedAt = _dateTimeProvider.UtcNow;
        report.AcceptedBy = currentUser;
        report.Status = ReportStatus.Accepted;

        await _nyssContext.SaveChangesAsync();

        var nonEssentialSubProcessesErrors = new List<string>();
        await SendReportsToDhis(
            reportId,
            nonEssentialSubProcessesErrors);

        return Success();
    }

    public async Task<Result> DismissReport(int reportId)
    {
        var currentUser = await _authorizationService.GetCurrentUser();
        var report = await _nyssContext.RawReports
            .Where(r => r.Id == reportId && r.Report != null)
            .Select(r => r.Report)
            .FirstOrDefaultAsync();

        if (report == null)
        {
            return Error(ResultKey.Report.ReportNotFound);
        }

        if (report.Status == ReportStatus.Rejected)
        {
            return Error(ResultKey.Report.AlreadyCrossChecked);
        }

        if (report.ReportType == ReportType.DataCollectionPoint)
        {
            return Error(ResultKey.Report.CannotCrossCheckDcpReport);
        }

        if (report.Location == null)
        {
            return Error(ResultKey.Report.CannotCrossCheckReportWithoutLocation);
        }

        report.RejectedAt = _dateTimeProvider.UtcNow;
        report.RejectedBy = currentUser;
        report.Status = ReportStatus.Rejected;

        await _nyssContext.SaveChangesAsync();

        var nonEssentialSubProcessesErrors = new List<string>();
        await SendReportsToDhis(
            reportId,
            nonEssentialSubProcessesErrors);

        return Success();
    }

    private async Task<IQueryable<RawReport>> BuildRawReportsBaseQuery(ReportListFilterRequestDto filter, int projectId)
    {
        if (filter.DataCollectorType == ReportListDataCollectorType.UnknownSender)
        {
            var nationalSocietyId = await _nyssContext.Projects
                .Where(p => p.Id == projectId)
                .Select(p => p.NationalSocietyId)
                .SingleOrDefaultAsync();

            return _nyssContext.RawReports
                .AsNoTracking()
                .Include(r => r.Report)
                .ThenInclude(r => r.ProjectHealthRisk)
                .ThenInclude(r => r.HealthRisk)
                .Where(r => r.NationalSociety.Id == nationalSocietyId)
                .FilterByDataCollectorType(filter.DataCollectorType)
                .FilterByHealthRisks(filter.HealthRisks)
                .FilterByErrorType(filter.ErrorType)
                .FilterByArea(filter.Locations)
                .FilterByDate(filter.StartDate, filter.EndDate.AddDays(1))
                .FilterByReportStatus(filter.ReportStatus)
                .FilterByTrainingMode(filter.TrainingStatus)
                .FilterByCorrectedState(filter.CorrectedState);
        }

        return _nyssContext.RawReports
            .AsNoTracking()
            .Include(r => r.Report)
            .ThenInclude(r => r.ProjectHealthRisk)
            .ThenInclude(r => r.HealthRisk)
            .FilterByProject(projectId)
            .FilterByHealthRisks(filter.HealthRisks)
            .FilterByDataCollectorType(filter.DataCollectorType)
            .FilterByArea(filter.Locations)
            .FilterByDate(filter.StartDate, filter.EndDate.AddDays(1))
            .FilterByErrorType(filter.ErrorType)
            .FilterByReportStatus(filter.ReportStatus)
            .FilterByTrainingMode(filter.TrainingStatus)
            .FilterByCorrectedState(filter.CorrectedState);
    }

    internal static void AnonymizeCrossOrganizationReports(
        IEnumerable<IReportListResponseDto> reports,
        string currentUserOrganizationName,
        StringsResourcesVault strings) =>
        reports
            .Where(r => r.IsAnonymized)
            .ToList()
            .ForEach(x =>
            {
                x.DataCollectorDisplayName = x.OrganizationName == currentUserOrganizationName
                    ? $"{strings[ResultKey.Report.LinkedToSupervisor]} {x.SupervisorName}"
                    : $"{strings[ResultKey.Report.LinkedToOrganization]} {x.OrganizationName}";
                x.PhoneNumber = "";
                x.Zone = "";
                x.Village = "";
            });

    private async Task SendReportsToDhis(
        int reportId,
        List<string> nonEssentialSubProcessesErrors)
    {
        try
        {
            await _dhisService.SendReportToDhis(reportId);
        }
        catch (ResultException e)
        {
            _loggerAdapter.Error(e, $"Failed to send reports to queue {_config.ServiceBusQueues.DhisReportQueue}.");
            nonEssentialSubProcessesErrors.Add(ResultKey.DhisIntegration.DhisApi.RegisterReportError);
        }
    }
}