using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bisuteria.Web.Models;

public sealed class TwoFactorViewModel
{
    [Required(ErrorMessage = "Introduce el código."), StringLength(32)]
    [Display(Name = "Código de verificación o recuperación")]
    public string Code { get; set; } = "";
    public bool Recovery { get; set; }
    public bool RememberMe { get; set; }
    [StringLength(2048)] public string? ReturnUrl { get; set; }
}

public sealed class AuthenticatorViewModel
{
    [Required(ErrorMessage = "Introduce tu contraseña actual."), DataType(DataType.Password), StringLength(256)]
    [Display(Name = "Contraseña actual")]
    public string Password { get; set; } = "";
    [StringLength(6), RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Introduce los seis dígitos de tu aplicación.")]
    [Display(Name = "Código de la aplicación")]
    public string? Code { get; set; }
    [BindNever] public string? SharedKey { get; set; }
    [BindNever] public bool Enabled { get; set; }
    [BindNever] public IEnumerable<string>? RecoveryCodes { get; set; }
}
