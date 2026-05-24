using Domain.Entities;
using Domain.Entities.Saga;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Context;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderSaga> OrderSagas { get; set; }
    public DbSet<SagaStep> SagaSteps { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // 👇 Единый стандарт: вынос конфигурации в отдельные классы
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
    }
}
