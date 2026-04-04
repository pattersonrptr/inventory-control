using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "The name is required.")]
    [StringLength(200, ErrorMessage = "The name must be at most 200 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(18, ErrorMessage = "The CNPJ must be at most 18 characters.")]
    [Display(Name = "CNPJ")]
    public string? Cnpj { get; set; }

    [StringLength(20, ErrorMessage = "The phone must be at most 20 characters.")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    [StringLength(100, ErrorMessage = "The email must be at most 100 characters.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
