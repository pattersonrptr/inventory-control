using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Nome Completo")]
    public string FullName { get; set; } = string.Empty;
}
