using Application.Shared.DTOs.External;

namespace Application.Shared.Interfaces;

/// <summary>
/// Клиент для взаимодействия с Product Service.
/// Используется для проверки существования продукта и резервирования стока.
/// </summary>
public interface IProductServiceClient
{
    /// <summary>
    /// Проверяет, существует ли продукт с указанным ID.
    /// </summary>
    /// <param name="productId">Идентификатор продукта</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>True если продукт существует, иначе false</returns>
    Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Резервирует товар на складе (для Saga-оркестрации).
    /// </summary>
    /// <param name="productId">Идентификатор продукта</param>
    /// <param name="quantity">Количество для резерва</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>True если резерв успешен, иначе false</returns>
    Task<bool> ReserveStockAsync(Guid productId, int quantity, CancellationToken ct = default);
    
    /// <summary>
    /// Возвращает продукт с указанным ID.
    /// </summary>
    /// <param name="productId">Идентификатор продукта</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>ProductDto</returns>
    Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default);
}
