# 🏗️ Блок 7. Микросервисная архитектура: Система управления заказами

## 🎯 Общее описание
Спроектируйте и реализуйте распределенную систему управления заказами, состоящую из двух микросервисов. Освойте ключевые концепции микросервисной архитектуры: границы контекстов, взаимодействие через HTTP, обеспечение надежности и наблюдаемости с использованием Saga паттерна.

**Стек:** .NET 8, Docker, PostgreSQL, Polly, xUnit

---

## 📋 Задание

### 🏗️ Часть 1: Проектирование микросервисов

#### 1.1 Определите границы контекстов:
- **Order Service**: Управление заказами, обработка жизненного цикла заказа
- **Product Service**: Управление каталогом товаров, проверка и резервирование стока

#### 1.2 Спроектируйте независимые модели данных:

**Order Service - сущности:**
```csharp
public class Order
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CorrelationId { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Confirmed,
    Cancelled,
    Failed
}
```

**Product Service - сущности:**
```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
}
```

### 🔄 Часть 2: Реализация REST API

#### 2.1 Order Service API (3 endpoints):

```
POST   /api/orders          - Создание нового заказа
GET    /api/orders/{id}     - Получение заказа по ID
GET    /api/orders/status/{id} - Получение статуса заказа
```

#### 2.2 Product Service API (3 endpoints):

```
GET    /api/products/{id}   - Получение информации о продукте
POST   /api/products/reserve - Резервирование стока для заказа
POST   /api/products/cancel-reserve - Отмена резервирования стока
```

### 🛡️ Часть 3: Взаимодействие и надежность

#### 3.1 Реализуйте HTTP клиент с Polly Retry (3 попытки, экспоненциальная задержка)

### 📊 Часть 4: Наблюдаемость

#### 4.1 Реализуйте middleware для correlationId и traceId:
#### 4.2 Настройте структурированное логирование в ваших микросервисах

### 💾 Часть 5: Реализация Saga паттерна

#### 5.1 Спроектируйте процесс создания заказа:

```
1. OrderService: Создание заказа в статусе "Pending"
2. OrderService → ProductService: Проверка и резервирование стока
3. ProductService: Резервирование стока (если доступно)
4. OrderService: Подтверждение заказа → "Confirmed"
5. (При ошибке) OrderService: Отмена → Компенсирующие действия
```

#### 5.2 Реализуйте оркестрируемую Saga в OrderService:

```csharp
public class OrderSaga
{
    public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public SagaState State { get; set; }
    public List<SagaStep> Steps { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CorrelationId { get; set; }
}

public enum SagaState
{
    Started,
    Processing,
    Completed,
    Compensating,
    Failed
}
```

#### 5.3 Реализуйте шаги Saga:

1. **CreateOrderStep** - создание заказа
2. **ReserveStockStep** - резервирование стока (синхронный вызов к ProductService)
3. **ConfirmOrderStep** - подтверждение заказа
4. **CompensationStep** - компенсирующие действия при ошибках

#### 5.4 Реализуйте компенсирующие транзакции:

- При ошибке резервирования стока → отмена заказа
- При таймауте ProductService → повторные попытки, затем отмена

### 🧪 Часть 6: Интеграционные тесты

#### 6.1 Настройте Testcontainers для тестовой среды.

#### 6.2 Напишите тесты с использованием Wiremock для сценариев:

1. **Успешное создание заказа**
2. **Создание заказа при недостаточном стоке** (ожидается ошибка)
3. **Обработка недоступности ProductService** (проверка retry логики)

---

## 📚 Ключевые концепции

### Разбиение домена
- Bounded Contexts и независимые модели данных
- Границы микросервисов по бизнес-возможностям
- Независимое развертывание

### Контракты и коммуникация
- Синхронное взаимодействие через HTTP REST
- Обработка сетевых ошибок и таймаутов

### Надежность
- Retry с экспоненциальной задержкой

### Наблюдаемость
- Correlation ID для сквозной трассировки
- Structured logging для анализа логов
- Trace ID для отслеживания запросов

### Управление данными
- Saga паттерн для распределенных транзакций
- Компенсирующие действия для отката

---

**Цель:** Освоить проектирование и реализацию распределенных систем с гарантиями надежности, правильным разделением ответственности и базовой наблюдаемостью.

**Результат:** Два работающих микросервиса с полным циклом создания заказа через Saga, готовые к запуску в Docker.

**Удачи в освоении микросервисной архитектуры! 🚀**
