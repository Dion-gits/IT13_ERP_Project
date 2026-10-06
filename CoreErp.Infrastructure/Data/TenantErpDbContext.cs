using CoreErp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreErp.Infrastructure.Data;

public class TenantErpDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public TenantErpDbContext(DbContextOptions<TenantErpDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.Role).HasMaxLength(50);
            entity.Property(x => x.Password).HasMaxLength(200);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.ProductId);
            entity.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            entity.HasIndex(x => x.ProductCode).IsUnique();
        });

        builder.Entity<Inventory>(entity =>
        {
            entity.HasKey(x => x.InventoryId);
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 2);

            entity.HasOne(x => x.Product)
                  .WithMany()
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Sale>(entity =>
        {
            entity.HasKey(x => x.SaleId);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CashierName).HasMaxLength(150);
            entity.Property(x => x.PaymentMethod).HasMaxLength(20);
            entity.Property(x => x.DiscountType).HasMaxLength(20);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.VatableSales).HasPrecision(18, 2);
            entity.Property(x => x.VatAmount).HasPrecision(18, 2);
            entity.Property(x => x.VatExemptSales).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.Property(x => x.ChangeDue).HasPrecision(18, 2);
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.CustomerIdNumber).HasMaxLength(50);
            entity.Property(x => x.PaymentReference).HasMaxLength(100);
        });

        builder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(x => x.SaleItemId);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.SubTotal).HasPrecision(18, 2);

            entity.HasOne(x => x.Sale)
                  .WithMany(s => s.SaleItems)
                  .HasForeignKey(x => x.SaleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                  .WithMany()
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}