using Domain.Entities;
using Domain.Entities.Saga;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// Конфигурация таблицы OrderSagas.
/// </summary>
public class OrderSagaConfiguration : IEntityTypeConfiguration<OrderSaga>
{
    public void Configure(EntityTypeBuilder<OrderSaga> entity)
    {
        entity.ToTable("OrderSagas");
        entity.HasKey(s => s.Id);
        
        // 👇 Связь 1:1 с Order
        entity.HasOne<Order>()
            .WithOne()
            .HasForeignKey<OrderSaga>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
       
        // 👇 Конвертация enum в строку
        entity.Property(s => s.State).HasConversion<string>();
        
        // 👇 Индексы
        entity.HasIndex(s => s.OrderId).IsUnique();  // 1:1
        entity.HasIndex(s => s.CorrelationId);
        
        // 👇 Ограничения на строки
        entity.Property(s => s.CorrelationId).HasMaxLength(100);
    }
}
