using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Database.Context;

/// <summary>
/// Фабрика для создания DbContext при дизайне (миграции, EF Core CLI).
/// Не используется в runtime — только для dotnet ef commands.
/// </summary>
public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Host=localhost;Database=CSharp-Ms-Order;Username=postgres;Password=password";

        optionsBuilder.UseNpgsql(connectionString, 
            b => b.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName));

        return new OrderDbContext(optionsBuilder.Options);
    }
}
