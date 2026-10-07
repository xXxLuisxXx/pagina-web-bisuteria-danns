using System.Linq.Expressions;
using Bisuteria.Application.DTOs.Catalog;
using Bisuteria.Application.DTOs.Cart;
using Bisuteria.Application.DTOs.Orders;
using Bisuteria.Application.DTOs.Blog;
using Bisuteria.Domain.Entities;

namespace Bisuteria.Application.Mappings;

/// <summary>Mapeos tipados y proyecciones SQL; sin descubrimiento ni licencia en ejecución.</summary>
public static class EntityMappings
{
    public static ProductCardDto ToCard(Product s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Slug = s.Slug,
        Description = s.Description,
        Price = s.Price,
        ComparePrice = s.ComparePrice,
        EffectivePrice = s.EffectivePrice,
        Stock = s.Stock,
        IsActive = s.IsActive,
        IsFeatured = s.IsFeatured,
        IsNew = s.IsNew,
        IsCustomizable = s.IsCustomizable,
        Material = s.Material,
        Colors = s.Colors,
        Size = s.Size,
        MainImageUrl = s.MainImageUrl,
        CategoryId = s.CategoryId,
        CategoryName = s.Category?.Name,
        SoldCount = s.SoldCount
    };
    public static ProductDetailDto ToDetail(Product s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Slug = s.Slug,
        Description = s.Description,
        Price = s.Price,
        ComparePrice = s.ComparePrice,
        EffectivePrice = s.EffectivePrice,
        Stock = s.Stock,
        IsActive = s.IsActive,
        IsFeatured = s.IsFeatured,
        IsNew = s.IsNew,
        IsCustomizable = s.IsCustomizable,
        Material = s.Material,
        Colors = s.Colors,
        Size = s.Size,
        MainImageUrl = s.MainImageUrl,
        CategoryId = s.CategoryId,
        CategoryName = s.Category?.Name,
        SoldCount = s.SoldCount,
        Sku = s.Sku ?? string.Empty,
        MinStock = s.MinStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Views = s.Views,
        CategorySlug = s.Category?.Slug,
        Images = s.Images.OrderBy(i => !i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(ToImage).ToList()
    };
    public static ProductImageDto ToImage(ProductImage s) => new()
    {
        Id = s.Id,
        ImageUrl = s.ImageUrl,
        AltText = s.AltText,
        IsPrimary = s.IsPrimary
    };
    public static ProductInputDto ToInput(Product s) => new()
    {
        OriginalStock = s.Stock,
        Images = s.Images.OrderBy(i => i.DisplayOrder).Select(ToImage).ToList(),
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        Price = s.Price,
        ComparePrice = s.ComparePrice,
        Material = s.Material,
        Colors = s.Colors,
        Size = s.Size,
        Sku = s.Sku,
        Stock = s.Stock,
        MinStock = s.MinStock,
        IsActive = s.IsActive,
        IsFeatured = s.IsFeatured,
        IsNew = s.IsNew,
        IsCustomizable = s.IsCustomizable,
        CategoryId = s.CategoryId
    };
    public static CustomizationInputDto ToInput(Customization s) => new()
    {
        ProductId = s.ProductId,
        Text = s.Text,
        Initials = s.Initials,
        Date = s.Date,
        Phrase = s.Phrase,
        Color = s.Color,
        Font = s.Font,
        Charm = s.Charm,
        Material = s.Material,
        Size = s.Size,
        Quantity = s.Quantity,
        GiftWrap = s.GiftWrap,
        Note = s.Note
    };
    public static OrderDto ToOrder(Order s) => new()
    {
        Id = s.Id,
        OrderNumber = s.OrderNumber,
        UserId = s.UserId,
        CustomerName = s.CustomerName,
        CustomerEmail = s.CustomerEmail,
        CustomerPhone = s.CustomerPhone,
        AddressLine1 = s.AddressLine1,
        AddressLine2 = s.AddressLine2,
        PostalCode = s.PostalCode,
        City = s.City,
        Province = s.Province,
        Observations = s.Observations,
        Status = s.Status,
        PaymentMethod = s.PaymentMethod,
        PaymentStatus = s.PaymentStatus,
        Subtotal = s.Subtotal,
        Discount = s.Discount,
        ShippingCost = s.ShippingCost,
        Total = s.Total,
        CreatedAt = s.CreatedAt,
        TrackingNumber = s.TrackingNumber,
        DeliveryMethod = s.DeliveryMethod,
        IsGift = s.IsGift,
        GiftRecipientName = s.GiftRecipientName,
        GiftMessage = s.GiftMessage,
        Items = s.Items.Select(ToOrderItem).ToList()
    };
    public static OrderItemDto ToOrderItem(OrderItem s) => new()
    {
        ProductId = s.ProductId ?? 0,
        ProductName = s.ProductName,
        ImageUrl = s.ImageUrl,
        UnitPrice = s.UnitPrice,
        Quantity = s.Quantity,
        LineTotal = s.LineTotal,
        CustomizationSummary = s.CustomizationSummary
    };
    public static readonly Expression<Func<Review, ReviewDto>> ReviewProjection = s => new()
    {
        Id = s.Id,
        CustomerName = s.CustomerName.Contains("@") ? "Cliente" : s.CustomerName,
        CustomerPhotoUrl = s.CustomerPhotoUrl,
        Rating = s.Rating,
        Title = s.Title,
        Comment = s.Comment,
        ReviewDate = s.ReviewDate
    };
    public static readonly Expression<Func<BlogPost, BlogPostDto>> BlogProjection = s => new()
    {
        Id = s.Id,
        Title = s.Title,
        Slug = s.Slug,
        Excerpt = s.Excerpt,
        Content = s.Content,
        CoverImage = s.CoverImage,
        Author = s.Author,
        Tags = s.Tags,
        PublishedAt = s.PublishedAt,
        Views = s.Views
    };
    public static readonly Expression<Func<GalleryImage, GalleryImageDto>> GalleryProjection = s => new()
    {
        Id = s.Id,
        ImageUrl = s.ImageUrl,
        Title = s.Title,
        Description = s.Description,
        Category = s.Category,
        IsFeatured = s.IsFeatured
    };
}
