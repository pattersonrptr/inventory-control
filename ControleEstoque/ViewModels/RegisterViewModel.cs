using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
    [Display(Name = "Nome Completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter entre 6 e 100 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar Senha")]
    [Compare("Password", ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "O perfil é obrigatório.")]
    [Display(Name = "Perfil")]
    public string Role { get; set; } = "Operator";
}
