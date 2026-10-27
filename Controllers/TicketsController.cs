using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>Lists tickets with optional filters, stable sorting, and bounded pagination.</summary>
    /// <remarks>Supported filters include status, priority, categoryId, requesterId, assigneeId, createdFrom, createdTo, and search.</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TicketResponse>>> GetTickets([FromQuery] TicketQuery query, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        return Ok(await ticketService.GetAllAsync(query, EmployeeRequesterId(userId), cancellationToken));
    }

    /// <summary>Gets a ticket by its identifier.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetTicketById(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var ticket = await ticketService.GetByIdAsync(id, EmployeeRequesterId(userId), cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    /// <summary>Creates a ticket and returns its resource location.</summary>
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> CreateTicket(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var ticket = await ticketService.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
    }

    /// <summary>Updates ticket details and validates the requested workflow transition.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Employee,Agent,Admin")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> UpdateTicket(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var (ticket, invalidTransition) = await ticketService.UpdateAsync(id, request, EmployeeRequesterId(userId), cancellationToken);
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
    [Authorize(Roles = "Agent,Admin")]
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
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var isEmployee = User.IsInRole(nameof(UserRole.Employee));
        var comments = await ticketService.GetCommentsAsync(id, isEmployee ? userId : null, !isEmployee, cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    /// <summary>Adds a public comment or internal note to a ticket.</summary>
    /// <remarks>The author is taken from the authenticated principal. Only agents and administrators can create internal notes.</remarks>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType<TicketCommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(int id, TicketCommentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var isEmployee = User.IsInRole(nameof(UserRole.Employee));
        if (isEmployee && request.IsInternal) return Forbid();
        var comment = await ticketService.AddCommentAsync(id, userId, isEmployee ? userId : null, request, cancellationToken);
        return comment is null ? NotFound() : Created($"/api/tickets/{id}/comments", comment);
    }

    /// <summary>Assigns a ticket to an active agent or administrator, or clears its assignment.</summary>
    /// <remarks>Send an assigneeId for assignment and null to unassign. Agents and administrators can use this route.</remarks>
    [HttpPut("{id:int}/assignment")]
    [Authorize(Roles = "Agent,Admin")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> AssignTicket(int id, TicketAssignmentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.AssignAsync(id, request, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    private bool TryGetCurrentUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    private int? EmployeeRequesterId(int userId) => User.IsInRole(nameof(UserRole.Employee)) ? userId : null;
}
