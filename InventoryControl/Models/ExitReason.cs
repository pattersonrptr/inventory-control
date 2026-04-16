using System.ComponentModel.DataAnnotations;

namespace InventoryControl.Models;

public enum ExitReason
{
    [Display(Name = "Venda")]
    Sale = 1,

    [Display(Name = "Perda")]
    Loss = 2,

    [Display(Name = "Devolução")]
    Return = 3,

    [Display(Name = "Outro")]
    Other = 4
}
