using System.Collections.Concurrent;
using System.Data;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Orders;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Esotera.Infrastructure.Services;

public class OrderService : IOrderService
{
    private const int IdempotencyKeyMaxLength = 64;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotencyLocks = new();

    private readonly EsoteraDbContext _context;
    private readonly ICouponService _couponService;
    private readonly IShippingOptionsService _shippingOptions;

    public OrderService(
        EsoteraDbContext context,
        ICouponService couponService,
        IShippingOptionsService shippingOptions)
    {
        _context = context;
        _couponService = couponService;
        _shippingOptions = shippingOptions;
    }

    public async Task<OrderDto> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        string idempotencyKey)
    {
        var key = NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = OrderIdempotencyFingerprint.Compute(request);
        var lockKey = $"{userId:N}:{key}";
        var gate = IdempotencyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync();
        try
        {
            var existing = await FindByIdempotencyAsync(userId, key);
            if (existing != null)
                return ResolveIdempotentReplay(existing, fingerprint);

            try
            {
                return await CreateNewOrderAsync(userId, request, key, fingerprint);
            }
            catch (DbUpdateException ex) when (IsCouponUsageUniqueViolation(ex))
            {
                throw new ConflictException("Você já utilizou este cupom.");
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                var raced = await FindByIdempotencyAsync(userId, key)
                    ?? throw new ConflictException(
                        "Não foi possível concluir o pedido. Tente novamente.");
                return ResolveIdempotentReplay(raced, fingerprint);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<OrderDto> CreateNewOrderAsync(
        Guid userId,
        CreateOrderRequest request,
        string idempotencyKey,
        string fingerprint)
    {
        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted);
        }

        try
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new NotFoundException("Usuário", userId);

            if (request.Items.Length > 20)
                throw new ValidationException("items", "Pedido pode conter no máximo 20 itens distintos.");

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(p => p.Images)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var orderItems = new List<OrderItem>();
            decimal subtotal = 0;

            foreach (var item in request.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    throw new NotFoundException("Produto", item.ProductId);

                if (product.IsArchived)
                    throw new ValidationException("items", $"Produto '{product.Name}' não está mais disponível para compra.");

                if (!product.IsAvailable)
                    throw new ValidationException("items", $"Produto '{product.Name}' não está disponível.");

                if (item.Quantity < 1 || item.Quantity > 99)
                    throw new ValidationException("items", "Quantidade deve estar entre 1 e 99.");

                var variations = ProductVariationJson.Parse(product.VariationsJson, product.Price);
                decimal unitPrice = product.Price;
                string? variationLabel = item.Variation;
                string? skuSnapshot = null;
                string? imageUrl = product.Images
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.SecureUrl)
                    .FirstOrDefault();

                if (variations.Length > 0)
                {
                    var selected = ProductVariationJson.Resolve(variations, item.Variation);
                    if (selected == null)
                        throw new ValidationException("items", $"Selecione uma variação válida para '{product.Name}'.");
                    if (!selected.IsAvailable)
                        throw new ValidationException("items", $"A variação '{selected.Name}' não está disponível.");

                    unitPrice = selected.Price;
                    variationLabel = selected.Name;
                    skuSnapshot = string.IsNullOrWhiteSpace(selected.Sku) ? null : selected.Sku.Trim();
                    if (!string.IsNullOrWhiteSpace(selected.ImageUrl))
                        imageUrl = selected.ImageUrl;
                }
                else
                {
                    skuSnapshot = string.IsNullOrWhiteSpace(product.Sku) ? null : product.Sku.Trim();
                    if (skuSnapshot == null)
                        throw new ValidationException(
                            "items",
                            $"Produto '{product.Name}' está sem SKU. Não é possível criar o pedido.");
                }

                var lineTotal = unitPrice * item.Quantity;
                subtotal += lineTotal;

                orderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = unitPrice,
                    Quantity = item.Quantity,
                    Variation = variationLabel,
                    Sku = skuSnapshot,
                    ImageUrl = imageUrl,
                    LineTotal = lineTotal
                });
            }

            OrderAddressInput address;
            if (request.AddressId.HasValue)
            {
                var savedAddress = await _context.Addresses
                    .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == userId)
                    ?? throw new NotFoundException("Endereço", request.AddressId);

                address = new OrderAddressInput(
                    savedAddress.Cep,
                    savedAddress.Street,
                    savedAddress.Number,
                    savedAddress.Complement,
                    savedAddress.Neighborhood,
                    savedAddress.City,
                    savedAddress.State,
                    savedAddress.IsResidentialAddress
                );
            }
            else if (request.Address != null)
            {
                address = request.Address;
            }
            else
            {
                throw new ValidationException("address", "Endereço é obrigatório.");
            }

            if (string.Equals(request.ShippingMethodId, ShippingMethod.J3, StringComparison.OrdinalIgnoreCase)
                && address.IsResidentialAddress is null)
            {
                throw new ValidationException(
                    "isResidentialAddress",
                    "Informe se o endereço é Residencial ou Comercial para usar a entrega J3.");
            }

            decimal discount = 0;
            string? couponCode = null;
            Coupon? lockedCoupon = null;

            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                lockedCoupon = await _couponService.LockAndValidateForOrderAsync(
                    userId, request.CouponCode, subtotal);
                discount = Math.Min(lockedCoupon.DiscountAmount, subtotal);
                couponCode = lockedCoupon.Code;
            }

            var settings = await _context.StoreSettings.FirstOrDefaultAsync()
                ?? StoreSettingsService.CreateDefault();

            var subtotalAfterDiscount = Math.Max(0, subtotal - discount);
            var cepDigits = new string(address.Cep.Where(char.IsDigit).ToArray());
            var shippingOption = await _shippingOptions.RequireOptionAsync(
                request.ShippingMethodId,
                new Application.Shipping.ShippingQuoteQuery(
                    cepDigits,
                    address.State,
                    subtotalAfterDiscount),
                settings);

            var shippingPrice = shippingOption.FinalPrice;
            // Pass-through: null = prazo desconhecido. Sem ?? 0 / GetValueOrDefault / default(int).
            var estimatedDays = shippingOption.EstimatedDaysMax;

            var total = subtotalAfterDiscount + Math.Max(0, shippingPrice);

            // Pagamento real (ou boleto): sempre aguarda confirmação via webhook/consulta MP.
            // Aprovação simulada existe apenas no frontend mock — nunca aqui na API.
            var initialStatus = OrderStatus.AwaitingPayment;
            var paymentStatus = "pending";

            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = GenerateOrderNumber(),
                UserId = userId,
                Status = initialStatus,
                Subtotal = subtotal,
                Discount = discount,
                ShippingPrice = shippingPrice,
                Total = total,
                CouponCode = couponCode,
                CouponId = lockedCoupon?.Id,
                CouponNominalDiscount = lockedCoupon?.DiscountAmount,
                CouponMinPurchaseSnapshot = lockedCoupon?.MinPurchase,
                CouponDiscountApplied = lockedCoupon != null ? discount : null,
                FreeShippingMinSnapshot = settings.FreeShippingMin,
                FreeShippingStatesSnapshot = settings.FreeShippingStatesCsv,
                J3PriceSnapshot = settings.J3Price,
                J3CutoffHourSnapshot = settings.J3CutoffHour,
                ShippingSubsidyEnabledSnapshot = settings.ShippingSubsidyEnabled,
                ShippingSubsidyAmountSnapshot = settings.ShippingSubsidyAmount,
                ShippingMethodId = shippingOption.ShippingMethodId,
                ShippingMethodName = ShippingMethod.GetDisplayName(shippingOption.ShippingMethodId),
                ShippingProvider = shippingOption.Provider,
                ShippingEstimatedDays = estimatedDays,
                ShippingCompanyId = shippingOption.CompanyId,
                ShippingServiceId = shippingOption.ServiceId,
                ShippingCarrierName = shippingOption.CarrierName,
                ShippingServiceName = shippingOption.ServiceName,
                ShippingOriginalPrice = shippingOption.OriginalPrice,
                ShippingDeliveryMinDays = shippingOption.EstimatedDaysMin,
                ShippingDeliveryMaxDays = shippingOption.EstimatedDaysMax,
                ShippingQuoteEnvironment = shippingOption.QuoteEnvironment,
                ShippingQuotedAtUtc = shippingOption.QuotedAtUtc,
                ShippingFreeShippingApplied = shippingOption.FreeShippingApplied,
                ShippingSubsidyApplied = shippingOption.SubsidyApplied,
                ShipCep = cepDigits,
                ShipStreet = address.Street.Trim(),
                ShipNumber = address.Number.Trim(),
                ShipComplement = address.Complement?.Trim(),
                ShipNeighborhood = address.Neighborhood.Trim(),
                ShipCity = address.City.Trim(),
                ShipState = address.State.Trim().ToUpperInvariant(),
                ShippingIsResidentialAddress = address.IsResidentialAddress,
                PaymentMethod = request.PaymentMethod,
                PaymentInstallments = request.PaymentMethod == PaymentMethod.Card
                    ? request.Installments
                    : null,
                PaymentStatus = paymentStatus,
                CustomerName = user.Name,
                CustomerEmail = user.Email,
                CustomerPhone = user.Phone,
                CustomerCpf = user.Cpf,
                IdempotencyKey = idempotencyKey,
                IdempotencyFingerprint = fingerprint,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            foreach (var item in orderItems)
                item.OrderId = order.Id;

            order.Items = orderItems;
            order.StatusHistory =
            [
                new OrderStatusHistory
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    FromStatus = null,
                    ToStatus = initialStatus,
                    CreatedAtUtc = DateTime.UtcNow
                }
            ];

            _context.Orders.Add(order);

            if (lockedCoupon != null)
            {
                _context.CouponUsages.Add(new CouponUsage
                {
                    Id = Guid.NewGuid(),
                    CouponId = lockedCoupon.Id,
                    UserId = userId,
                    OrderId = order.Id,
                    UsedAtUtc = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            if (transaction != null)
                await transaction.CommitAsync();

            return MapToDto(order);
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<Order?> FindByIdempotencyAsync(Guid userId, string key)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == key);
    }

    private static OrderDto ResolveIdempotentReplay(Order existing, string fingerprint)
    {
        if (!string.Equals(existing.IdempotencyFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Os dados desta tentativa foram alterados. Revise o pedido e tente novamente.");
        }

        return MapToDto(existing);
    }

    private static string NormalizeIdempotencyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ValidationException("idempotencyKey", "Cabeçalho Idempotency-Key é obrigatório.");

        var trimmed = key.Trim();
        if (trimmed.Length > IdempotencyKeyMaxLength)
            throw new ValidationException(
                "idempotencyKey",
                $"Idempotency-Key deve ter no máximo {IdempotencyKeyMaxLength} caracteres.");

        return trimmed;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pg)
            return pg.SqlState == PostgresErrorCodes.UniqueViolation;

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCouponUsageUniqueViolation(DbUpdateException ex)
    {
        if (!IsUniqueViolation(ex))
            return false;

        var message = (ex.InnerException?.Message ?? ex.Message).ToLowerInvariant();
        return message.Contains("couponusages")
            || message.Contains("ix_couponusages_couponid_userid")
            || (ex.InnerException is PostgresException pg
                && (pg.ConstraintName?.Contains("CouponUsage", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    public async Task<IReadOnlyList<OrderListDto>> ListMineAsync(Guid userId)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderListDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.Items.Count,
                o.CustomerName,
                o.CreatedAtUtc
            ))
            .ToListAsync();
    }

    public async Task<OrderDto?> GetMineAsync(Guid userId, Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory.OrderBy(h => h.CreatedAtUtc))
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        return order == null ? null : MapToDto(order);
    }

    public async Task<PagedResult<OrderListDto>> AdminListAsync(OrderFilterRequest filter)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(o => o.Status == filter.Status);

        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search) ||
                o.CustomerName.ToLower().Contains(search) ||
                o.CustomerEmail.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => new OrderListDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.Items.Count,
                o.CustomerName,
                o.CreatedAtUtc
            ))
            .ToListAsync();

        return new PagedResult<OrderListDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<OrderDto?> AdminGetAsync(Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory.OrderBy(h => h.CreatedAtUtc))
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto> UpdateStatusAsync(
        Guid orderId,
        UpdateOrderStatusRequest request,
        Guid changedByUserId)
    {
        if (!OrderStatus.IsValid(request.Status))
            throw new ValidationException("status", "Status inválido.");

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new NotFoundException("Pedido", orderId);

        if (request.ExpectedVersion.HasValue && order.RowVersion != request.ExpectedVersion.Value)
        {
            throw new ConflictException(
                "O pedido foi alterado por outro usuário. Recarregue e tente novamente.");
        }

        var fromStatus = order.Status;
        order.Status = request.Status;
        order.UpdatedAtUtc = DateTime.UtcNow;
        order.RowVersion += 1;

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = request.Status,
            ChangedByUserId = changedByUserId,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "O pedido foi alterado por outro usuário. Recarregue e tente novamente.");
        }

        var refreshed = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstAsync(o => o.Id == orderId);

        return MapToDto(refreshed);
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmm");
        var random = Random.Shared.Next(1000, 9999);
        return $"ES{timestamp}{random}";
    }

    private static OrderDto MapToDto(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.Status,
        order.Subtotal,
        order.Discount,
        order.ShippingPrice,
        order.Total,
        order.CouponCode,
        new OrderShippingDto(
            order.ShippingMethodId,
            order.ShippingMethodName,
            order.ShippingProvider,
            order.ShippingEstimatedDays
        ),
        new OrderPaymentDto(
            order.PaymentMethod,
            order.PaymentInstallments,
            order.PaymentStatus
        ),
        new OrderCustomerDto(
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone,
            order.CustomerCpf
        ),
        new OrderAddressDto(
            order.ShipCep,
            order.ShipStreet,
            order.ShipNumber,
            order.ShipComplement,
            order.ShipNeighborhood,
            order.ShipCity,
            order.ShipState
        ),
        order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductName,
            i.UnitPrice,
            i.Quantity,
            i.Variation,
            i.ImageUrl,
            i.LineTotal,
            i.Sku
        )).ToArray(),
        order.StatusHistory.OrderBy(h => h.CreatedAtUtc).Select(h => new OrderStatusHistoryDto(
            h.FromStatus,
            h.ToStatus,
            h.Note,
            h.CreatedAtUtc
        )).ToArray(),
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.RowVersion
    );
}
