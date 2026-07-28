using Esotera.Application.DTOs.Coupons;
using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

public interface ICouponService
{
    Task<CouponValidationResponse> ValidateAsync(Guid userId, string code, decimal subtotal);

    /// <summary>
    /// Carrega o cupom com bloqueio pessimista (FOR UPDATE), valida e retorna a entidade
    /// pronta para consumo na mesma transação do pedido.
    /// </summary>
    Task<Coupon> LockAndValidateForOrderAsync(Guid userId, string code, decimal subtotal);

    Task<IReadOnlyList<AdminCouponDto>> AdminListAsync(bool? isArchived = null, bool? isActive = null);
    Task<AdminCouponDto?> AdminGetAsync(Guid id);
    Task<AdminCouponDto> AdminCreateAsync(CreateCouponRequest request);
    Task<AdminCouponDto> AdminUpdateAsync(Guid id, UpdateCouponRequest request);
    Task<AdminCouponDto> AdminSetActiveAsync(Guid id, bool isActive);
    Task<AdminCouponDto> AdminArchiveAsync(Guid id);
    Task<AdminCouponDto> AdminRestoreAsync(Guid id);
}
