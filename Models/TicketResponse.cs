namespace HelpDesk.Api.Models;

public sealed record TicketResponse(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    int? CategoryId,
    int? RequesterId,
    int? AssigneeId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt);
