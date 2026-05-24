using Application;
using Infrastructure;
using Infrastructure.Database.Seeders;
using Serilog;
using Prometheus;
using Web;
using Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 🔧 Serilog: структурированное логирование
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrderService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 🔧 Dependency Injection
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWeb(builder.Configuration);

// 🔧 Exceptions: регистрируем обработчик
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddProblemDetails(options => 
{
    options.CustomizeProblemDetails = ctx => 
    {
        ctx.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
    };
});


// 🔧 CORS (для разработки)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});


var app = builder.Build();

// 🔧 Инициализация БД: миграции + сидинг
await DatabaseInitializer.InitializeAsync(app.Services, builder.Configuration, app.Lifetime.ApplicationStopping);

// 🔧 Middleware pipeline
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// ✅ Корреляция ID для трассировки
app.UseMiddleware<CorrelationIdMiddleware>();


// ✅ Встроенная обработка через IExceptionHandler
app.UseExceptionHandler(); 

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1"));
}

// перед app.MapControllers():
app.UseHttpMetrics();  // Автоматические метрики HTTP-запросов
app.MapMetrics().AllowAnonymous();      // Эндпоинт /metrics для Prometheus должен быть анонимным

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
