using System.Text;
using Esotera.Application.DTOs.Newsletter;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/newsletter")]
[Authorize(Roles = "Admin")]
public class AdminNewsletterController : ControllerBase
{
    private readonly INewsletterService _newsletter;

    public AdminNewsletterController(INewsletterService newsletter)
    {
        _newsletter = newsletter;
    }

    [HttpGet]
    public async Task<ActionResult<NewsletterAdminListResponse>> List(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _newsletter.AdminListAsync(search, isActive, skip, take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var csv = await _newsletter.AdminExportCsvAsync(search, isActive, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"newsletter-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}