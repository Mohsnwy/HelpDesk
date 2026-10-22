using HelpDesk.Api.Models;
using HelpDesk.Api.Services;

namespace HelpDesk.Api.Tests;

public class TicketWorkflowTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Reopened, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Reopened, true)]
    [InlineData(TicketStatus.Reopened, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress, false)]
    public void CanTransition_enforces_the_ticket_workflow(TicketStatus current, TicketStatus next, bool expected) =>
        Assert.Equal(expected, TicketWorkflow.CanTransition(current, next));
}
