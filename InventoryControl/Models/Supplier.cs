using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(200, ErrorMessage = "O nome deve ter no máximo 200 caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(18, ErrorMessage = "O CNPJ deve ter no máximo 18 caracteres.")]
    [Display(Name = "CNPJ")]
    public string? Cnpj { get; set; }

    [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
    [Display(Name = "Telefone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(100, ErrorMessage = "O e-mail deve ter no máximo 100 caracteres.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
