using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.DTOs.Admin;

/// <summary>Resumen del dashboard principal.</summary>
public class DashboardStats
{
    public decimal MonthlyGoal { get; set; }
    public decimal CalendarSales { get; set; }
    public IReadOnlyList<ChartPoint> CalendarDays { get; set; } = [];
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Interval { get; set; } = "day";
    public OrderStatus? RecentStatus { get; set; }
    public decimal PeriodSales { get; set; }
    public decimal PreviousSales { get; set; }
    public int PeriodOrders { get; set; }
    public int PreviousOrders { get; set; }
    public int NewClients { get; set; }
    public int PreviousClients { get; set; }
    public decimal AverageTicket => PeriodOrders == 0 ? 0 : PeriodSales / PeriodOrders;
    public decimal PreviousTicket => PreviousOrders == 0 ? 0 : PreviousSales / PreviousOrders;
    public decimal PreviousDaySales { get; set; }
    public decimal PreviousMonthSales { get; set; }
    public IReadOnlyList<ChartPoint> SalesByCategory { get; set; } = [];
    public IReadOnlyList<ChartPoint> SalesByPayment { get; set; } = [];
    public IReadOnlyList<DashboardAlert> Alerts { get; set; } = [];

    public decimal SalesToday { get; set; }

    public int OrdersToday { get; set; }

    public decimal SalesMonth { get; set; }

    public int OrdersMonth { get; set; }

    public int PendingOrders { get; set; }

    public int ProductsSold { get; set; }

    public int RegisteredClients { get; set; }

    public int TotalProducts { get; set; }

    /// <summary>Productos con stock disponible (&gt; 0).</summary>
    public int AvailableProducts { get; set; }

    /// <summary>Productos agotados (stock = 0).</summary>
    public int SoldOutProducts { get; set; }

    /// <summary>Categorías registradas.</summary>
    public int CategoriesCount { get; set; }

    /// <summary>Total de pedidos históricos.</summary>
    public int TotalOrders { get; set; }

    /// <summary>Ventas totales históricas (pedidos no cancelados).</summary>
    public decimal TotalSales { get; set; }

    public int LowStockCount { get; set; }

    public int PendingReviews { get; set; }

    public int UnreadMessages { get; set; }

    /// <summary>Ventas de los últimos 12 meses (etiqueta, valor).</summary>
    public IReadOnlyList<ChartPoint> SalesByMonth { get; set; } = Array.Empty<ChartPoint>();

    /// <summary>Pedidos por estado.</summary>
    public IReadOnlyList<ChartPoint> OrdersByStatus { get; set; } = Array.Empty<ChartPoint>();

    /// <summary>Top de productos vendidos.</summary>
    public IReadOnlyList<TopProductDto> TopProducts { get; set; } = Array.Empty<TopProductDto>();

    public IReadOnlyList<OrderDto> RecentOrders { get; set; } = Array.Empty<OrderDto>();
}

public sealed record AdminSearchResult(string Kind, string Label, string Url);

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Valor")]
    public decimal Value { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "URL de imagen")]
    public string? ImageUrl { get; set; }

    public int QuantitySold { get; set; }

    public decimal Revenue { get; set; }
}

/// <summary>Línea de reporte de ventas para exportación.</summary>
public class SalesReportRow
{
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public string Customer { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public int Items { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal Shipping { get; set; }

    public decimal Total { get; set; }
}

/// <summary>Línea de reporte de productos para exportación.</summary>
public class ProductReportRow
{
    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Categoría")]
    public string Category { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public int Sold { get; set; }

    public decimal Revenue { get; set; }
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public int OrderCount { get; set; }

    public decimal TotalSpent { get; set; }
}

/// <summary>Mensaje recibido a través del formulario de contacto.</summary>
public class ContactMessageDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Reseña para moderación en el panel.</summary>
public class ReviewAdminDto
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductImageUrl { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public int Rating { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Título")]
    public string? Title { get; set; }

    public string Comment { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTime ReviewDate { get; set; }
}

/// <summary>Cupón de descuento (panel administrativo).</summary>
public class CouponDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    public DiscountType Type { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Valor")]
    public decimal Value { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Importe mínimo")]
    public decimal MinAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Límite de usos")]
    public int? MaxUses { get; set; }

    public int UsedCount { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Fecha de inicio")]
    public DateTime? StartsAt { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Fecha de vencimiento")]
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    public string? Description { get; set; }
}

public class CouponInputDto
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Código")]
    [System.ComponentModel.DataAnnotations.StringLength(40, ErrorMessage = "{0} admite hasta 40 caracteres.")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "{0} es obligatorio.")]
    public string Code { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(DiscountType), ErrorMessage = "Selecciona un tipo de descuento válido.")]
    public DiscountType Type { get; set; } = DiscountType.Percentage;

    [System.ComponentModel.DataAnnotations.Display(Name = "Valor")]
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "9999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "{0} debe estar entre 0.01 y 9999999.")]
    public decimal Value { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Importe mínimo")]
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "9999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "{0} debe estar entre 0 y 9999999.")]
    public decimal MinAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Límite de usos")]
    [System.ComponentModel.DataAnnotations.Range(typeof(int), "1", "2147483647", ErrorMessage = "{0} debe estar entre 1 y 2147483647.")]
    public int? MaxUses { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Fecha de inicio")]
    public DateTime? StartsAt { get; set; }

    [System.ComponentModel.DataAnnotations.Display(Name = "Fecha de vencimiento")]
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Display(Name = "Descripción")]
    [System.ComponentModel.DataAnnotations.StringLength(1000, ErrorMessage = "{0} admite hasta 1000 caracteres.")]
    public string? Description { get; set; }
}

/// <summary>
/// Fila del historial de movimientos de inventario.
/// </summary>
public class StockMovementRow
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int StockAfter { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed record DashboardAlert(string Label, int Count, string Url);
