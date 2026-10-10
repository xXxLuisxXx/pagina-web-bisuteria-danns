using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public sealed class EmailConfirmationViewModel
{
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [Required, StringLength(4096)] public string Token { get; set; } = "";
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Introduce tu contraseña actual."), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Introduce la nueva contraseña."), StringLength(100, MinimumLength = 12, ErrorMessage = "Usa entre 12 y 100 caracteres."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
