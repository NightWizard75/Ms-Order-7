using Domain.Entities.Saga;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// Конфигурация таблицы SagaSteps для EF Core.
/// Использует явный внешний ключ SagaId (не shadow property).
/// </summary>
public class SagaStepConfiguration : IEntityTypeConfiguration<SagaStep>
{
    public void Configure(EntityTypeBuilder<SagaStep> entity)
    {
        // 👇 Имя таблицы в БД
        entity.ToTable("SagaSteps");
        
        // 👇 Первичный ключ
        entity.HasKey(s => s.Id);
        
        // 👇 Явный внешний ключ на OrderSaga (1:N связь)
        entity.HasOne<OrderSaga>()
            .WithMany(s => s.Steps)
            .HasForeignKey(s => s.SagaId)  // 👈 Ссылка на явное свойство
            .OnDelete(DeleteBehavior.Cascade);  // Если Saga удалена — удаляем шаги
        
        // 👇 Конвертация enum SagaStepStatus в строку (для читаемости в БД)
        entity.Property(s => s.Status).HasConversion<string>();
        
        // 👇 Ограничения на длину строк (для производительности и валидации)
        entity.Property(s => s.StepName).HasMaxLength(100).IsRequired();
        entity.Property(s => s.ErrorMessage).HasMaxLength(500);
        entity.Property(s => s.CompensationResult).HasMaxLength(500);
        
        // 👇 Индексы для ускорения поиска
        entity.HasIndex(s => s.SagaId);  // Быстрый поиск шагов по Saga
        entity.HasIndex(s => s.Status);  // Фильтрация по статусу шага
        
        // 👇 Настройки по умолчанию (опционально, но полезно)
        entity.Property(s => s.RetryCount).HasDefaultValue(0);
        entity.Property(s => s.TimeoutSeconds).HasDefaultValue(30);
    }
}
