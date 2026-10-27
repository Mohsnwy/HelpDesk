using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

public sealed class TicketCommentRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

public sealed record TicketCommentResponse(int Id, int TicketId, int AuthorId, string Body, bool IsInternal, DateTime CreatedAt);

public sealed class TicketAssignmentRequest
{
    public int? AssigneeId { get; set; }
}
