using System.ComponentModel.DataAnnotations;

namespace Bisuteria.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(256, ErrorMessage = "La contraseña admite hasta 256 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    [StringLength(2048, ErrorMessage = "La dirección de retorno admite hasta 2048 caracteres.")]
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [Display(Name = "Nombre")]
    [StringLength(100, ErrorMessage = "El campo no puede superar 100 caracteres.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [Display(Name = "Apellido")]
    [StringLength(100, ErrorMessage = "El campo no puede superar 100 caracteres.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono inválido.")]
    [Display(Name = "Teléfono")]
    [StringLength(40, ErrorMessage = "El campo no puede superar 40 caracteres.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(2048, ErrorMessage = "La dirección de retorno admite hasta 2048 caracteres.")]
    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [Display(Name = "Nombre")]
    [StringLength(100, ErrorMessage = "El campo no puede superar 100 caracteres.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [Display(Name = "Apellido")]
    [StringLength(100, ErrorMessage = "El campo no puede superar 100 caracteres.")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Teléfono inválido.")]
    [Display(Name = "Teléfono")]
    [StringLength(40, ErrorMessage = "El campo no puede superar 40 caracteres.")]
    public string? Phone { get; set; }

    [Display(Name = "Ciudad")]
    [StringLength(120, ErrorMessage = "El campo no puede superar 120 caracteres.")]
    public string? City { get; set; }

    [Display(Name = "Provincia")]
    [StringLength(120, ErrorMessage = "El campo no puede superar 120 caracteres.")]
    [EcuadorProvince]
    public string? Province { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    [Display(Name = "Correo electrónico")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [StringLength(200, ErrorMessage = "El campo no puede superar 200 caracteres.")]
    [EmailAddress(ErrorMessage = "Introduce un correo válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [StringLength(4096, ErrorMessage = "El campo no puede superar 4096 caracteres.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirma la contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
