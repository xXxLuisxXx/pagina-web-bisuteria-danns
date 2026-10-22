using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public sealed class AdminUserEditViewModel
{
    [Required, StringLength(450)]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Introduce un correo electrónico válido.")]
    [StringLength(256)]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)]
    [Display(Name = "Nombre")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio."), StringLength(100)]
    [Display(Name = "Apellido")]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Introduce un teléfono válido."), StringLength(40)]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [StringLength(120, ErrorMessage = "La ciudad admite hasta 120 caracteres.")]
    [Display(Name = "Ciudad")]
    public string? City { get; set; }

    [StringLength(120, ErrorMessage = "La provincia admite hasta 120 caracteres.")]
    [EcuadorProvince]
    [Display(Name = "Provincia")]
    public string? Province { get; set; }
}
