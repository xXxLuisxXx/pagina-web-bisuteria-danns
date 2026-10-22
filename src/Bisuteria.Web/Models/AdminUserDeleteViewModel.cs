using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public sealed class AdminUserDeleteViewModel
{
    [Required, StringLength(450)]
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el correo del cliente para confirmar.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [Display(Name = "Confirma el correo del cliente")]
    public string ConfirmationEmail { get; set; } = string.Empty;
}
