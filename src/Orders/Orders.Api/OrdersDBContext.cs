using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;

namespace Orders.Api;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<OrderSummary> OrderSummaries => Set<OrderSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.SKU)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();
        
        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(o => o.ProcessedAt);

        modelBuilder.Entity<OrderSummary>()
        .HasIndex(o => o.OrderId)
        .IsUnique();

        modelBuilder.Entity<OrderSummary>()
            .HasIndex(o => o.CustomerId);
    }
}