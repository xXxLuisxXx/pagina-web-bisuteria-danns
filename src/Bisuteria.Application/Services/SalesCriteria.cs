using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.Services;

/// <summary>Importes de pedidos no cancelados; no equivale a dinero cobrado.</summary>
public static class SalesCriteria
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    public static IQueryable<Order> Apply(IQueryable<Order> orders, DateTime? from = null, DateTime? to = null, PaymentStatus? payment = null)
    {
        var query = orders.Where(o => o.Status != OrderStatus.Cancelled);
        if (from.HasValue) { var start = Utc(from.Value.Date); query = query.Where(o => o.CreatedAt >= start); }
        if (to.HasValue) { var end = Utc(to.Value.Date.AddDays(1)); query = query.Where(o => o.CreatedAt < end); }
        if (payment.HasValue) query = query.Where(o => o.PaymentStatus == payment);
        return query;
    }
}
