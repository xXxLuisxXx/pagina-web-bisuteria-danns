using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public sealed class StaffInvitationViewModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string RoleName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;
}
