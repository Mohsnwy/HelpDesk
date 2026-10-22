using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

public class UpdateTicketRequest
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

    [Required]
    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public int? CategoryId { get; set; }
}
