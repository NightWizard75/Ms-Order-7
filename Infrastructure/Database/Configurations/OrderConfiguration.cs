using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        // 👇 Конфигурация таблицы через лямбду
        entity.ToTable("Orders", tableBuilder =>
        {
            // 👇 Проверки на уровне БД настраиваются через TableBuilder
            tableBuilder.HasCheckConstraint("CK_Order_PriceInKopecks_NonNegative",
                $"""
                 "{nameof(Order.PriceInKopecks)}" >= 0
                 """);
            tableBuilder.HasCheckConstraint("CK_Order_Quantity_Positive", 
                $"""
                 "{nameof(Order.Quantity)}" > 0
                 """);
        });
        
        entity.HasKey(e => e.Id);
        
        // 👇 Индексы
        entity.HasIndex(e => e.CorrelationId);
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.CustomerEmail);
        
        // 👇 Свойства
        entity.Property(e => e.Id).IsRequired();
        entity.Property(e => e.ProductId).IsRequired();
        entity.Property(e => e.Quantity).IsRequired();
        entity.Property(e => e.PriceInKopecks)
            .IsRequired()
            .HasDefaultValue(0);
        entity.Property(e => e.TotalAmountInKopecks)
            
            .IsRequired();
        entity.Property(e => e.Status).IsRequired();
        entity.Property(e => e.CustomerEmail)
            .IsRequired()
            .HasMaxLength(255);
        entity.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);
        entity.Property(e => e.CreatedAt).IsRequired();
    }
}
