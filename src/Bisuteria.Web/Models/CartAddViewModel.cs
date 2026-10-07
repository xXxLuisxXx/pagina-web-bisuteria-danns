using System.ComponentModel.DataAnnotations;
using Bisuteria.Application.DTOs.Cart;

namespace Bisuteria.Web.Models;

/// <summary>Modelo de datos del formulario "Añadir al carrito".</summary>
public class CartAddViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Producto inválido.")]
    public int ProductId { get; set; }

    [Range(1, 50, ErrorMessage = "Cantidad inválida.")]
    public int Quantity { get; set; } = 1;

    public CustomizationInputDto Customization { get; set; } = new();
}
