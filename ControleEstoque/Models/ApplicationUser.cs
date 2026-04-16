using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Nome Completo")]
    public string FullName { get; set; } = string.Empty;
}
