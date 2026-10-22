using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public sealed class ShippingZoneInputModel : IValidatableObject
{
    [Range(0, int.MaxValue, ErrorMessage = "La zona no es válida.")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Selecciona una ciudad.")]
    [Display(Name = "Ciudad")]
    public string City { get; set; } = "Manta";

    [Required(ErrorMessage = "El sector es obligatorio.")]
    [StringLength(120, ErrorMessage = "El sector no puede superar 120 caracteres.")]
    [Display(Name = "Sector")]
    public string Sector { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "10000", ParseLimitsInInvariantCulture = true, ErrorMessage = "La tarifa debe estar entre 0 y 10 000 USD.")]
    [Display(Name = "Tarifa de envío (USD)")]
    public decimal Cost { get; set; }

    [Display(Name = "Envío gratis")]
    public bool IsFree { get; set; }

    [Display(Name = "Costo por confirmar con la tienda")]
    public bool RequiresConfirmation { get; set; } = true;

    [StringLength(160, ErrorMessage = "El tiempo estimado no puede superar 160 caracteres.")]
    [Display(Name = "Tiempo estimado (opcional)")]
    public string? EstimatedTime { get; set; }

    [Display(Name = "Zona activa")]
    public bool IsActive { get; set; } = true;

    [Range(0, 10000, ErrorMessage = "El orden debe estar entre 0 y 10 000.")]
    [Display(Name = "Orden de aparición")]
    public int SortOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (City is not ("Manta" or "Jaramijó"))
            yield return new ValidationResult("Solo se permiten Manta y Jaramijó, Manabí.", [nameof(City)]);

        if (IsFree && RequiresConfirmation)
            yield return new ValidationResult("Una zona no puede ser gratuita y tener el costo por confirmar al mismo tiempo.", [nameof(IsFree), nameof(RequiresConfirmation)]);

        if ((IsFree || RequiresConfirmation) && Cost != 0m)
            yield return new ValidationResult("La tarifa debe ser 0 si el envío es gratis o está por confirmar.", [nameof(Cost)]);
        else if (!IsFree && !RequiresConfirmation && Cost <= 0m)
            yield return new ValidationResult("Ingresa una tarifa mayor que 0 o marca el envío como gratis o por confirmar.", [nameof(Cost)]);

        if (decimal.Round(Cost, 2) != Cost)
            yield return new ValidationResult("La tarifa admite como máximo dos decimales.", [nameof(Cost)]);
    }
}
