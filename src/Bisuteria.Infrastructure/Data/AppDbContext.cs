using Bisuteria.Domain.Entities;
using Bisuteria.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Data;

/// <summary>
/// Contexto de base de datos de la aplicación (EF Core + Identity).
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customization> Customizations => Set<Customization>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.ComparePrice).HasPrecision(18, 2);
            e.Property(x => x.Material).HasMaxLength(120);
            e.Property(x => x.Colors).HasMaxLength(300);
            e.Property(x => x.Size).HasMaxLength(60);
            e.Property(x => x.Sku).HasMaxLength(60);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Sku);
            e.HasIndex(x => x.Sku, "UX_Products_Sku").IsUnique().HasFilter("[Sku] IS NOT NULL");
            e.HasOne(x => x.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductImage>(e =>
        {
            e.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Product)
             .WithMany(p => p.Images)
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(e =>
        {
            e.HasIndex(x => x.CheckoutToken).IsUnique().HasFilter("[CheckoutToken] IS NOT NULL");
            e.Property(x => x.CouponCode).HasMaxLength(60);
            e.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
            e.Property(x => x.CustomerEmail).HasMaxLength(200).IsRequired();
            e.Property(x => x.CustomerPhone).HasMaxLength(40).IsRequired();
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.Province).HasMaxLength(120);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.ShippingCost).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.Status);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product)
             .WithMany()
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Customization)
             .WithMany()
             .HasForeignKey(x => x.CustomizationId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Customization>(e =>
        {
            e.Property(x => x.Color).HasMaxLength(60);
            e.Property(x => x.Font).HasMaxLength(60);
            e.Property(x => x.Charm).HasMaxLength(120);
            e.Property(x => x.Material).HasMaxLength(120);
            e.Property(x => x.Size).HasMaxLength(60);
            e.Property(x => x.PriceOverride).HasPrecision(18, 2);
        });

        builder.Entity<Favorite>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            e.HasOne(x => x.Product)
             .WithMany()
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Review>(e =>
        {
            e.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne(x => x.Product)
             .WithMany(p => p.Reviews)
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StockMovement>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(300);
            e.HasOne(x => x.Product)
             .WithMany()
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Reference).HasMaxLength(120);
            e.HasOne(x => x.Order)
             .WithMany(o => o.Payments)
             .HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Address>(e =>
        {
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<BlogPost>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(220).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(240).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<Coupon>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Value).HasPrecision(18, 2);
            e.Property(x => x.MinAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<ContactMessage>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        });

        builder.Entity<GalleryImage>(e =>
        {
            e.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80);
        });

        builder.Entity<SiteSetting>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(1000);
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
