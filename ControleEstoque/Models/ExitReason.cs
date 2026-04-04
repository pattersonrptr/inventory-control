using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

public enum ExitReason
{
    [Display(Name = "Sale")]
    Sale = 1,

    [Display(Name = "Loss")]
    Loss = 2,

    [Display(Name = "Return")]
    Return = 3,

    [Display(Name = "Other")]
    Other = 4
}
