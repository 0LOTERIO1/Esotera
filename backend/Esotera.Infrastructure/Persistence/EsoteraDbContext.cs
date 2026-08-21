using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Persistence;

public class EsoteraDbContext : DbContext
{
    public EsoteraDbContext(DbContextOptions<EsoteraDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NewsletterSubscription> NewsletterSubscriptions => Set<NewsletterSubscription>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MelhorEnvioConnection> MelhorEnvioConnections => Set<MelhorEnvioConnection>();
    public DbSet<MelhorEnvioOAuthState> MelhorEnvioOAuthStates => Set<MelhorEnvioOAuthState>();
    public DbSet<J3Fulfillment> J3Fulfillments => Set<J3Fulfillment>();
    public DbSet<FiscalInvoice> FiscalInvoices => Set<FiscalInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EsoteraDbContext).Assembly);
    }
}
