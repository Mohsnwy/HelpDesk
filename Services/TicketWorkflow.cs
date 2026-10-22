using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public static class TicketWorkflow
{
    public static bool CanTransition(TicketStatus current, TicketStatus next) =>
        (current, next) switch
        {
            (TicketStatus.Open, TicketStatus.InProgress) => true,
            (TicketStatus.InProgress, TicketStatus.Resolved) => true,
            (TicketStatus.Resolved, TicketStatus.Closed) => true,
            (TicketStatus.Resolved or TicketStatus.Closed, TicketStatus.Reopened) => true,
            (TicketStatus.Reopened, TicketStatus.InProgress) => true,
            _ => false
        };
}
