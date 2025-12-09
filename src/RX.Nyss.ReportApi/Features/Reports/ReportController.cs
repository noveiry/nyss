using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RX.Nyss.ReportApi.Features.Reports.Contracts;
using RX.Nyss.ReportApi.Features.Reports.Models;

namespace RX.Nyss.ReportApi.Features.Reports;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // Report comes from SMSEagle/Nyss manual button and is forwarded to Nyss
    [HttpPost]
    [Route("registerReport")]
    public async Task<IActionResult> Post([FromBody] Report report) =>
        await _reportService.ReceiveReport(report)
            ? (StatusCodeResult)new OkResult()
            : new BadRequestResult();

    // Report comes from Nyss Escalate process and is forwarded to EidsrAPi
    [HttpPost]
    [Route("registerEidsrEvent")]
    public async Task<IActionResult> RegisterEidsrEvent([FromBody] EidsrReport eidsrReport) =>
        await _reportService.RegisterEidsrEvent(eidsrReport)
            ? (StatusCodeResult)new OkResult()
            : new BadRequestResult();

    // Report comes from accept report process and is forwarded to EidsrAPi
    [HttpPost]
    [Route("registerDhisReport")]
    public async Task<IActionResult> RegisterDhisReport([FromBody] DhisReport dhisReport) =>
        await _reportService.RegisterDhisReport(dhisReport)
            ? (StatusCodeResult)new OkResult()
            : new BadRequestResult();

    // Report comes from Telerivet and is forwarded to Nyss
    [HttpPost]
    [Route("telerivetReport")]
    public async Task<IActionResult> ReceiveTelerivetReport([FromBody] TelerivetReport t) =>
        await _reportService.ReceiveTelerivetReport(t)
            ? (StatusCodeResult)new OkResult()
            : new BadRequestResult();


    // Report comes from MTN and is forwarded to Nyss
    [HttpPost]
    [Route("mtnReport")]
    public async Task<IActionResult> ReceiveMTNReport([FromBody] MTNReport mr) =>
        await _reportService.ReceiveMTNReport(mr)
            ? (StatusCodeResult)new OkResult()
            : new BadRequestResult();

}
