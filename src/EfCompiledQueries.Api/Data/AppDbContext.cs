using EfCompiledQueries.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCompiledQueries.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table names are explicit so scripts/SeedData.sql can mirror the model.
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("Customers");
            customer.Property(c => c.Name).HasMaxLength(200);
            customer.Property(c => c.Email).HasMaxLength(320);
            customer.Property(c => c.City).HasMaxLength(100);
            customer.HasIndex(c => c.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("Products");
            product.Property(p => p.Sku).HasMaxLength(32);
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.UnitPrice).HasPrecision(18, 2);
            product.HasIndex(p => p.Sku).IsUnique();
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("Orders");
            order.Property(o => o.TotalAmount).HasPrecision(18, 2);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            order.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer lookup is the list endpoint. Status + date supports the
            // infrequent admin query. These indexes help SQL Server, which is a
            // separate decision from compiling the LINQ query.
            order.HasIndex(o => o.CustomerId);
            order.HasIndex(o => new { o.Status, o.OrderedAtUtc });
        });

        modelBuilder.Entity<OrderItem>(item =>
        {
            item.ToTable("OrderItems");
            item.Property(i => i.UnitPrice).HasPrecision(18, 2);
            item.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            item.HasOne(i => i.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // The product-sales aggregate joins every line to its product.
            // An index on ProductId helps that scan more than a compiled delegate does.
            item.HasIndex(i => i.ProductId);
        });
    }
}
