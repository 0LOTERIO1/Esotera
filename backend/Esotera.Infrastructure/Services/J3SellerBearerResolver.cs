using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Resolve Bearer para mutations Seller: login credentials → SellerAuthProvider; senão J3_TOKEN legado.
/// </summary>
internal static class J3SellerBearerResolver
{
    public static async Task<(string? Token, string? ErrorCode)> ResolveAsync(
        J3ShippingOptions options,
        IJ3SellerAuthProvider auth,
        CancellationToken cancellationToken)
    {
        if (options.HasSellerLoginCredentials)
        {
            var result = await auth.GetAccessTokenAsync(cancellationToken);
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                return (
                    null,
                    J3FulfillmentErrorCodes.Sanitize(result.ErrorCode)
                    ?? J3FulfillmentErrorCodes.AuthLoginFailed);
            }

            return (result.AccessToken, null);
        }

        if (!string.IsNullOrWhiteSpace(options.Token))
            return (options.Token.Trim(), null);

        return (null, J3FulfillmentErrorCodes.Configuration);
    }
}
