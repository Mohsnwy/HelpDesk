namespace HelpDesk.Api.Models;

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed,
    Reopened
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? RequesterId { get; set; }
    public User? Requester { get; set; }
    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
}
