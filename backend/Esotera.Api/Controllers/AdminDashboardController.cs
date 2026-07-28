using Esotera.Application.DTOs.Admin;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminQueryService _adminQueries;

    public AdminDashboardController(IAdminQueryService adminQueries)
    {
        _adminQueries = adminQueries;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> Dashboard()
    {
        var dashboard = await _adminQueries.GetDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<AdminCustomerDto>>> Customers()
    {
        var customers = await _adminQueries.ListCustomersAsync();
        return Ok(customers);
    }

    [HttpGet("sales/products")]
    public async Task<ActionResult<IReadOnlyList<AdminSoldProductDto>>> SoldProducts()
    {
        var products = await _adminQueries.GetSoldProductsAsync();
        return Ok(products);
    }
}
