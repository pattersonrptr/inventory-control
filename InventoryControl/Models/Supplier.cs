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

    [StringLength(200, ErrorMessage = "O nome do contato deve ter no máximo 200 caracteres.")]
    [Display(Name = "Contato")]
    public string? ContactName { get; set; }

    [Range(0, 365, ErrorMessage = "O prazo de entrega deve ser entre 0 e 365 dias.")]
    [Display(Name = "Prazo de Entrega (dias)")]
    public int? LeadTimeDays { get; set; }

    [StringLength(1000, ErrorMessage = "As observações devem ter no máximo 1000 caracteres.")]
    [Display(Name = "Observações")]
    public string? Notes { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
