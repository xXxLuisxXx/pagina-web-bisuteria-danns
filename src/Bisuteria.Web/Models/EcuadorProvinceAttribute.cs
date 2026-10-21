using System.ComponentModel.DataAnnotations;
using Bisuteria.Domain.Enums;

namespace Bisuteria.Web.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class EcuadorProvinceAttribute : ValidationAttribute
{
    public EcuadorProvinceAttribute() : base("Selecciona una provincia del Ecuador.") { }

    public override bool IsValid(object? value)
        => value is null || value is string province &&
            (string.IsNullOrWhiteSpace(province) || EcuadorProvinces.Contains(province));
}
