using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

/// <summary>
/// Smoke test admin: login Seller + validação sellerId. Zero mutations J3.
/// Não depende de J3_FULFILLMENT_ENABLED / J3_IMPORT_BY_ACCESS_KEY_ENABLED.
/// </summary>
[ApiController]
[Route("api/admin/j3")]
[Authorize(Roles = "Admin")]
public sealed class AdminJ3AuthController : ControllerBase
{
    private const string AuthModeSellerLogin = "seller_login";
    private const string GenericFailureCode = "J3_SELLER_AUTH_FAILED";

    private readonly IJ3SellerAuthProvider _sellerAuth;

    public AdminJ3AuthController(IJ3SellerAuthProvider sellerAuth)
    {
        _sellerAuth = sellerAuth;
    }

    [HttpGet("auth-check")]
    public async Task<ActionResult<J3SellerAuthCheckResponse>> AuthCheck(
        CancellationToken cancellationToken)
    {
        J3SellerAuthResult result;
        try
        {
            result = await _sellerAuth.GetAccessTokenAsync(cancellationToken);
        }
        catch
        {
            return StatusCode(StatusCodes.Status502BadGateway, Failure(GenericFailureCode));
        }

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            return Ok(new J3SellerAuthCheckResponse
            {
                Success = true,
                Authenticated = true,
                SellerValidated = true,
                AuthMode = AuthModeSellerLogin
            });
        }

        var code = J3FulfillmentErrorCodes.Sanitize(result.ErrorCode) ?? GenericFailureCode;
        return StatusCode(StatusCodes.Status502BadGateway, Failure(code));
    }

    private static J3SellerAuthCheckResponse Failure(string errorCode) => new()
    {
        Success = false,
        Authenticated = false,
        SellerValidated = false,
        ErrorCode = errorCode
    };
}
