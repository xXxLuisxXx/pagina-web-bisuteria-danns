using System.Security.Cryptography;
using System.Text;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Entities;
using Bisuteria.Domain.Enums;
using Bisuteria.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Bisuteria.Infrastructure.Services;

public sealed class PaymentProofService(IUnitOfWork unitOfWork, IOrderService orders) : IPaymentProofService
{
    public async Task<bool> CanAccessOrderAsync(string orderNumber, string? userId, string? accessToken, CancellationToken ct = default)
    {
        var order = await unitOfWork.Repository<Order>().Query().AsNoTracking()
            .Where(x => x.OrderNumber == orderNumber)
            .Select(x => new { x.UserId, x.GuestAccessTokenHash })
            .SingleOrDefaultAsync(ct);
        if (order is null) return false;
        if (!string.IsNullOrWhiteSpace(userId) && string.Equals(order.UserId, userId, StringComparison.Ordinal)) return true;
        if (!string.IsNullOrWhiteSpace(order.UserId) || string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 128) return false;
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
        return !string.IsNullOrWhiteSpace(order.GuestAccessTokenHash) && order.GuestAccessTokenHash.Length == actual.Length &&
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(order.GuestAccessTokenHash), Encoding.ASCII.GetBytes(actual));
    }

    public async Task<IReadOnlyList<PaymentProofDto>> GetForOrderAsync(string orderNumber, CancellationToken ct = default)
        => await unitOfWork.Repository<PaymentProof>().Query().AsNoTracking()
            .Where(x => x.Order!.OrderNumber == orderNumber)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PaymentProofDto(x.Id, x.Order!.OrderNumber, x.ContentType, x.Length, x.Status, x.CreatedAt, x.ReviewNote))
            .ToListAsync(ct);

    public async Task<PaymentProofDto?> GetAsync(int proofId, CancellationToken ct = default)
        => await unitOfWork.Repository<PaymentProof>().Query().AsNoTracking()
            .Where(x => x.Id == proofId)
            .Select(x => new PaymentProofDto(x.Id, x.Order!.OrderNumber, x.ContentType, x.Length, x.Status, x.CreatedAt, x.ReviewNote))
            .SingleOrDefaultAsync(ct);

    public Task<string?> GetStorageKeyAsync(int proofId, CancellationToken ct = default)
        => unitOfWork.Repository<PaymentProof>().Query().AsNoTracking().Where(x => x.Id == proofId)
            .Select(x => x.StorageKey).SingleOrDefaultAsync(ct);

    public Task<int?> AddAsync(string orderNumber, string? userId, string? accessToken, string storageKey,
        string contentType, long length, CancellationToken ct = default)
        => unitOfWork.InTransactionAsync<int?>(async () =>
        {
            var order = await unitOfWork.Repository<Order>().Query().Include(x => x.PaymentProofs)
                .SingleOrDefaultAsync(x => x.OrderNumber == orderNumber, ct);
            if (order is null || !await CanAccessOrderAsync(orderNumber, userId, accessToken, ct) ||
                order.PaymentMethod != PaymentMethod.BankTransfer || order.ShippingToConfirm ||
                order.PaymentStatus != PaymentStatus.Pending || order.Status == OrderStatus.Cancelled ||
                order.PaymentProofs.Count >= 3 || storageKey.Length != 32 || length is <= 0 or > 5 * 1024 * 1024 ||
                contentType is not ("image/jpeg" or "application/pdf")) return null;

            var proof = new PaymentProof
            {
                OrderId = order.Id, StorageKey = storageKey, ContentType = contentType,
                Length = length, Status = PaymentProofStatus.Pending
            };
            await unitOfWork.Repository<PaymentProof>().AddAsync(proof, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return proof.Id;
        }, ct);

    public Task<bool> ReviewAsync(int proofId, bool approve, string? note, string actorId, string actorRole, CancellationToken ct = default)
        => unitOfWork.InTransactionAsync(async () =>
        {
            var proof = await unitOfWork.Repository<PaymentProof>().Query().Include(x => x.Order)
                .SingleOrDefaultAsync(x => x.Id == proofId, ct);
            if (proof?.Order is null || proof.Status != PaymentProofStatus.Pending || string.IsNullOrWhiteSpace(actorId) ||
                actorRole is not (StoreRoles.Owner or StoreRoles.Developer or StoreRoles.LegacyAdmin) ||
                (!approve && string.IsNullOrWhiteSpace(note)) || note?.Length > 500) return false;

            var order = proof.Order;
            if (approve && (order.PaymentMethod != PaymentMethod.BankTransfer || order.ShippingToConfirm ||
                            order.PaymentStatus != PaymentStatus.Pending || order.Status == OrderStatus.Cancelled)) return false;

            proof.Status = approve ? PaymentProofStatus.Approved : PaymentProofStatus.Rejected;
            proof.ReviewedByUserId = actorId;
            proof.ReviewedAtUtc = DateTime.UtcNow;
            proof.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            unitOfWork.Repository<PaymentProof>().Update(proof);

            if (approve)
            {
                if (!await orders.UpdatePaymentStatusAsync(order.Id, PaymentStatus.Paid, ct)) return false;
                if (order.Status == OrderStatus.Pending && !await orders.UpdateStatusAsync(order.Id, OrderStatus.Confirmed, cancellationToken: ct)) return false;
            }

            await unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                ActorId = actorId, ActorRole = actorRole,
                Action = approve ? "PaymentProof.Approved" : "PaymentProof.Rejected",
                EntityType = "PaymentProof", EntityId = proof.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Outcome = "Success",
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { proof.OrderId, proof.Status })
            }, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
}
