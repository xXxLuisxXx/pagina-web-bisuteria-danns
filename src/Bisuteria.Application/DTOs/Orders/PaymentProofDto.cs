using Bisuteria.Domain.Enums;

namespace Bisuteria.Application.DTOs.Orders;

public sealed record PaymentProofDto(int Id, string OrderNumber, string ContentType, long Length,
    PaymentProofStatus Status, DateTime CreatedAt, string? ReviewNote);
