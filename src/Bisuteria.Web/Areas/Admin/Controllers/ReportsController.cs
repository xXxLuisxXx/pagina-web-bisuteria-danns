using System.Globalization;
using System.Text;
using Bisuteria.Application.Interfaces;
using Bisuteria.Domain.Enums;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace Bisuteria.Web.Areas.Admin.Controllers;

public class ReportsController : AdminBaseController
{
    private readonly IAdminService _admin;

    public ReportsController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Reportes";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Sales(DateTime? from = null, DateTime? to = null, OrderStatus? status = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "Reporte de ventas";
        var rows = status.HasValue
            ? await _admin.GetReportByPeriodAsync(status, ct)
            : await _admin.GetSalesReportAsync(from, to, ct);

        if (from.HasValue)
            rows = rows.Where(r => r.Date.Date >= from.Value.Date).ToList();
        if (to.HasValue)
            rows = rows.Where(r => r.Date.Date <= to.Value.Date).ToList();

        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Status = status;
        ViewBag.Statuses = Enum.GetValues<OrderStatus>();
        ViewBag.Total = rows.Sum(r => r.Total);
        ViewBag.Count = rows.Count;
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Products(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "Reporte de productos";
        var rows = await _admin.GetProductReportAsync(from, to, ct);
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.TotalRevenue = rows.Sum(r => r.Revenue);
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportSales(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var rows = await _admin.GetSalesReportAsync(from, to, ct);
        if (from.HasValue)
            rows = rows.Where(r => r.Date.Date >= from.Value.Date).ToList();
        if (to.HasValue)
            rows = rows.Where(r => r.Date.Date <= to.Value.Date).ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Ventas");

        sheet.Cell(1, 1).Value = "Pedido";
        sheet.Cell(1, 2).Value = "Fecha";
        sheet.Cell(1, 3).Value = "Cliente";
        sheet.Cell(1, 4).Value = "Provincia";
        sheet.Cell(1, 5).Value = "Estado";
        sheet.Cell(1, 6).Value = "Pago";
        sheet.Cell(1, 7).Value = "Artículos";
        sheet.Cell(1, 8).Value = "Subtotal";
        sheet.Cell(1, 9).Value = "Descuento";
        sheet.Cell(1, 10).Value = "Envío";
        sheet.Cell(1, 11).Value = "Total";

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#8B6914");
        header.Style.Font.FontColor = XLColor.White;

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.OrderNumber;
            sheet.Cell(row, 2).Value = r.Date;
            sheet.Cell(row, 3).Value = r.Customer;
            sheet.Cell(row, 4).Value = r.Province;
            sheet.Cell(row, 5).Value = r.Status;
            sheet.Cell(row, 6).Value = r.PaymentMethod;
            sheet.Cell(row, 7).Value = r.Items;
            sheet.Cell(row, 8).Value = (double)r.Subtotal;
            sheet.Cell(row, 9).Value = (double)r.Discount;
            sheet.Cell(row, 10).Value = (double)r.Shipping;
            sheet.Cell(row, 11).Value = (double)r.Total;
            row++;
        }

        sheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"ventas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportProducts(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var rows = await _admin.GetProductReportAsync(from, to, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Productos");

        sheet.Cell(1, 1).Value = "Producto";
        sheet.Cell(1, 2).Value = "Categoría";
        sheet.Cell(1, 3).Value = "SKU";
        sheet.Cell(1, 4).Value = "Precio";
        sheet.Cell(1, 5).Value = "Stock";
        sheet.Cell(1, 6).Value = "Vendidos";
        sheet.Cell(1, 7).Value = "Ingresos";

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#8B6914");
        header.Style.Font.FontColor = XLColor.White;

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.Name;
            sheet.Cell(row, 2).Value = r.Category;
            sheet.Cell(row, 3).Value = r.Sku;
            sheet.Cell(row, 4).Value = (double)r.Price;
            sheet.Cell(row, 5).Value = r.Stock;
            sheet.Cell(row, 6).Value = r.Sold;
            sheet.Cell(row, 7).Value = (double)r.Revenue;
            row++;
        }

        sheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"productos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string report, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        if (report == "sales")
        {
            var rows = await _admin.GetSalesReportAsync(from, to, ct);
            sb.AppendLine("Pedido;Fecha;Cliente;Provincia;Estado;Pago;Artículos;Subtotal;Descuento;Envío;Total");
            foreach (var r in rows)
                sb.AppendLine($"{r.OrderNumber};{r.Date:dd/MM/yyyy};{Csv(r.Customer)};{Csv(r.Province)};{r.Status};{r.PaymentMethod};{r.Items};{r.Subtotal.ToString("F2", CultureInfo.InvariantCulture)};{r.Discount.ToString("F2", CultureInfo.InvariantCulture)};{r.Shipping.ToString("F2", CultureInfo.InvariantCulture)};{r.Total.ToString("F2", CultureInfo.InvariantCulture)}");
        }
        else
        {
            var rows = await _admin.GetProductReportAsync(from, to, ct);
            sb.AppendLine("Producto;Categoría;SKU;Precio;Stock;Vendidos;Ingresos");
            foreach (var r in rows)
                sb.AppendLine($"{Csv(r.Name)};{Csv(r.Category)};{Csv(r.Sku)};{r.Price.ToString("F2", CultureInfo.InvariantCulture)};{r.Stock};{r.Sold};{r.Revenue.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"reporte_{report}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
