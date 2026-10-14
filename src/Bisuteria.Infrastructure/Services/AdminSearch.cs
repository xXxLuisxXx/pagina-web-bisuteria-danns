using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Application.Services;
using Microsoft.EntityFrameworkCore;
namespace Bisuteria.Infrastructure.Services;

public partial class AdminService
{
    public async Task<IReadOnlyList<AdminSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (query.Length > 200) throw new BusinessRuleException("La búsqueda admite hasta 200 caracteres.");
        var term = query.Trim();
        if (term.Length < 2) return [];
        var orders = await _context.Orders.AsNoTracking().Where(o => o.OrderNumber.Contains(term) || o.CustomerName.Contains(term) || o.CustomerEmail.Contains(term))
            .OrderByDescending(o => o.CreatedAt).Take(10).Select(o => new { o.OrderNumber, o.CustomerName }).ToListAsync(cancellationToken);
        var products = await _context.Products.AsNoTracking().Where(p => p.Name.Contains(term) || (p.Sku != null && p.Sku.Contains(term)))
            .OrderBy(p => p.Name).ThenBy(p => p.Id).Take(10).Select(p => new { p.Id, p.Name, p.Sku }).ToListAsync(cancellationToken);
        return orders.Select(o => new AdminSearchResult("Pedido", $"{o.OrderNumber} · {o.CustomerName}", "/Admin/Orders/Details?orderNumber=" + Uri.EscapeDataString(o.OrderNumber)))
            .Concat(products.Select(p => new AdminSearchResult("Producto", $"{p.Name} · {p.Sku}", $"/Admin/Products/Edit/{p.Id}"))).ToList();
    }
}
