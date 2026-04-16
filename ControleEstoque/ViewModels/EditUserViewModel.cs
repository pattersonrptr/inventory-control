using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.ViewModels;

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(100)]
    [Display(Name = "Nome Completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O perfil é obrigatório.")]
    [Display(Name = "Perfil")]
    public string Role { get; set; } = "Operator";
}
