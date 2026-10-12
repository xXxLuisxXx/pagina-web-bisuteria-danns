using Bisuteria.Application.Interfaces;
using Bisuteria.Application.Services;
using Bisuteria.Application.DTOs.Admin;
using Bisuteria.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ClosedXML.Excel;
using System.Text;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class DashboardController(IAdminService admin, IMemoryCache cache) : AdminBaseController
{
    private Task<DashboardStats> Load(DateTime? from, DateTime? to, string interval, OrderStatus? status, CancellationToken ct)
        => cache.GetOrCreateAsync($"dashboard:{from:yyyyMMdd}:{to:yyyyMMdd}:{interval}:{status}", entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return admin.GetDashboardAsync(ct, from, to, interval, status);
        })!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct, DateTime? from = null, DateTime? to = null, string interval = "day", OrderStatus? status = null)
    {
        ViewData["Title"] = "Panel de administración";
        try { return View(await Load(from, to, interval, status, ct)); }
        catch (BusinessRuleException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken ct, DateTime? from = null, DateTime? to = null, string interval = "day", string format = "csv")
    {
        if (format is not ("csv" or "xlsx")) return BadRequest("Formato inválido.");
        DashboardStats data;
        try { data = await Load(from, to, interval, null, ct); }
        catch (BusinessRuleException ex) { return BadRequest(ex.Message); }
        var rows = new List<(string Section, string Label, decimal Value)> {
            ("KPI", "Importe del periodo", data.PeriodSales), ("KPI", "Importe anterior", data.PreviousSales),
            ("KPI", "Pedidos", data.PeriodOrders), ("KPI", "Ticket promedio", data.AverageTicket), ("KPI", "Clientes nuevos", data.NewClients)
        };
        foreach (var (name, points) in new[] { ("Serie", data.SalesByMonth), ("Categoría (bruto)", data.SalesByCategory), ("Método", data.SalesByPayment), ("Estado", data.OrdersByStatus) })
            rows.AddRange(points.Select(p => (name, p.Label, p.Value)));
        rows.AddRange(data.TopProducts.Select(p => ("Top productos (unidades)", p.Name, (decimal)p.QuantitySold)));
        if (format == "xlsx") {
            using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Dashboard");
            sheet.Cell(1, 1).Value = "Sección"; sheet.Cell(1, 2).Value = "Concepto"; sheet.Cell(1, 3).Value = "Valor";
            var line = 2;
            foreach (var row in rows) { sheet.Cell(line, 1).Value = SpreadsheetText.Safe(row.Section); sheet.Cell(line, 2).Value = SpreadsheetText.Safe(row.Label); sheet.Cell(line++, 3).Value = row.Value; }
            sheet.Columns().AdjustToContents(); using var stream = new MemoryStream(); book.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "dashboard.xlsx");
        }
        string Cell(string text) => "\"" + SpreadsheetText.Safe(text).Replace("\"", "\"\"") + "\"";
        var csv = "Sección,Concepto,Valor\r\n" + string.Join("\r\n", rows.Select(r => $"{Cell(r.Section)},{Cell(r.Label)},{r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", "dashboard.csv");
    }
}
