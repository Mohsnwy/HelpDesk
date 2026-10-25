using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>Lists tickets with optional filters, stable sorting, and bounded pagination.</summary>
    /// <remarks>Supported filters include status, priority, categoryId, requesterId, assigneeId, createdFrom, createdTo, and search.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TicketResponse>>> GetTickets([FromQuery] TicketQuery query, CancellationToken cancellationToken) =>
        Ok(await ticketService.GetAllAsync(query, cancellationToken));

    /// <summary>Gets a ticket by its identifier.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetTicketById(int id, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetByIdAsync(id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    /// <summary>Creates a ticket and returns its resource location.</summary>
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> CreateTicket(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
    }

    /// <summary>Updates ticket details and validates the requested workflow transition.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> UpdateTicket(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var (ticket, invalidTransition) = await ticketService.UpdateAsync(id, request, cancellationToken);
        if (invalidTransition)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Invalid ticket status transition",
                Detail = "The requested status change is not allowed by the ticket workflow."
            });
        }

        return ticket is null ? NotFound() : Ok(ticket);
    }

    /// <summary>Deletes a ticket.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicket(int id, CancellationToken cancellationToken) =>
        await ticketService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Lists comments for a ticket in creation order.</summary>
    [HttpGet("{id:int}/comments")]
    [ProducesResponseType<IReadOnlyList<TicketCommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketCommentResponse>>> GetComments(int id, CancellationToken cancellationToken)
    {
        var comments = await ticketService.GetCommentsAsync(id, cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    /// <summary>Adds a public comment or internal note to a ticket.</summary>
    /// <remarks>Until authentication is implemented, the request supplies an existing active AuthorId. IsInternal is returned for future agent-only enforcement.</remarks>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType<TicketCommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(int id, TicketCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await ticketService.AddCommentAsync(id, request, cancellationToken);
        return comment is null ? NotFound() : Created($"/api/tickets/{id}/comments", comment);
    }

    /// <summary>Assigns a ticket to an active agent or administrator, or clears its assignment.</summary>
    /// <remarks>Send an assigneeId for assignment and null to unassign. Role authorization is added with authentication in Stage 8.</remarks>
    [HttpPut("{id:int}/assignment")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> AssignTicket(int id, TicketAssignmentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.AssignAsync(id, request, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }
}
