using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public class ContactViewModel
{
    [Required(ErrorMessage = "Tu nombre es obligatorio.")]
    [Display(Name = "Nombre")]
    [StringLength(160, ErrorMessage = "El campo no puede superar 160 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono inválido.")]
    [Display(Name = "Teléfono")]
    [StringLength(40, ErrorMessage = "El campo no puede superar 40 caracteres.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [Display(Name = "Asunto")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre 10 y 2000 caracteres.")]
    [Display(Name = "Mensaje")]
    public string Message { get; set; } = string.Empty;
}
