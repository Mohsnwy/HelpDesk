
using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

public class CreateTicketRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public int? CategoryId { get; set; }
}
