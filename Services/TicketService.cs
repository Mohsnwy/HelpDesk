using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Services;

public class TicketService(HelpDeskDbContext dbContext) : ITicketService
{
    public async Task<IReadOnlyList<TicketResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Tickets.AsNoTracking().OrderBy(ticket => ticket.Id)
            .Select(ticket => ToResponse(ticket)).ToListAsync(cancellationToken);

    public async Task<TicketResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.AsNoTracking().SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
        return ticket is null ? null : ToResponse(ticket);
    }

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            CategoryId = request.CategoryId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(ticket);
    }

    public async Task<(TicketResponse? Ticket, bool InvalidTransition)> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
        if (ticket is null) return (null, false);
        if (ticket.Status != request.Status && !TicketWorkflow.CanTransition(ticket.Status, request.Status))
            return (null, true);
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Priority = request.Priority;
        ticket.CategoryId = request.CategoryId;
        ticket.Status = request.Status;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.ResolvedAt = request.Status == TicketStatus.Resolved
            ? ticket.ResolvedAt ?? ticket.UpdatedAt
            : request.Status == TicketStatus.Reopened ? null : ticket.ResolvedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (ToResponse(ticket), false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
        if (ticket is null) return false;
        dbContext.Tickets.Remove(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id, ticket.Title, ticket.Description, ticket.Status, ticket.Priority,
        ticket.CategoryId, ticket.RequesterId, ticket.AssigneeId,
        ticket.CreatedAt, ticket.UpdatedAt, ticket.ResolvedAt);

    private async Task ValidateCategoryAsync(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not null && !await dbContext.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
            throw new TicketInputException($"Category {categoryId} does not exist.");
    }
}
