using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "public", tb => 
            tb.HasComment("Transactional Outbox: События, ожидающие публикации в шину сообщений"));
        
        builder.HasKey(x => x.Id);

        // 🔹 Поля 
        builder.Property(x => x.OrderId)
            .IsRequired()
            .HasComment("Идентификатор заказа (связь без навигационного свойства)");
            
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(255)
            .HasComment("Полное имя типа события (для десериализации)");
            
        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasComment("JSON-сериализованное тело события (PostgreSQL jsonb)");
            
        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("Время создания сообщения (фиксация на уровне БД)");
            
        builder.Property(x => x.PublishedAt)
            .IsRequired(false)
            .HasComment("Время успешной публикации. NULL = ещё не опубликовано");

        // 🔹 Индекс для быстрого поиска неопубликованных сообщений
        builder
            .HasIndex(x => new { x.PublishedAt, x.CreatedAt })
            .HasDatabaseName("ix_outbox_messages_published_at_created_at");
    }
}
