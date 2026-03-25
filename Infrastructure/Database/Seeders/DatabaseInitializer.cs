using Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database.Seeders;

public class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider, 
        IConfiguration configuration, 
        CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    
        // 👇 Применить миграции
        await context.Database.MigrateAsync(ct);
    
        // 👇 Сидинг тестовых данных
        // await SeedAsync(context, ct);
    }
}
