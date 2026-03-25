namespace Web.Middleware;

/// <summary>
/// Middleware для генерации и проброса Correlation ID.
/// Добавляет заголовок X-Correlation-Id ко всем запросам и ответам.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // 🔍 Берём ID из заголовка или генерируем новый
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // ➕ Добавляем в контекст для логирования
        context.Items["CorrelationId"] = correlationId;

        // 📤 Пробрасываем в ответ
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // 🔄 Передаём управление дальше
        await next(context);
    }
}
