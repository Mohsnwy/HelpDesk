using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Services;

public class TicketService(HelpDeskDbContext dbContext) : ITicketService
{
    public async Task<PagedResult<TicketResponse>> GetAllAsync(TicketQuery query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        IQueryable<Ticket> tickets = dbContext.Tickets.AsNoTracking();
        if (query.Status is not null) tickets = tickets.Where(ticket => ticket.Status == query.Status);
        if (query.Priority is not null) tickets = tickets.Where(ticket => ticket.Priority == query.Priority);
        if (query.CategoryId is not null) tickets = tickets.Where(ticket => ticket.CategoryId == query.CategoryId);
        if (query.RequesterId is not null) tickets = tickets.Where(ticket => ticket.RequesterId == query.RequesterId);
        if (query.AssigneeId is not null) tickets = tickets.Where(ticket => ticket.AssigneeId == query.AssigneeId);
        if (query.CreatedFrom is not null) tickets = tickets.Where(ticket => ticket.CreatedAt >= query.CreatedFrom);
        if (query.CreatedTo is not null) tickets = tickets.Where(ticket => ticket.CreatedAt <= query.CreatedTo);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            tickets = tickets.Where(ticket => EF.Functions.ILike(ticket.Title, $"%{search}%") || EF.Functions.ILike(ticket.Description, $"%{search}%"));
        }

        var totalCount = await tickets.CountAsync(cancellationToken);
        var descending = query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        tickets = query.SortBy.ToLowerInvariant() switch
        {
            "id" => descending ? tickets.OrderByDescending(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.Id),
            "title" => descending ? tickets.OrderByDescending(ticket => ticket.Title).ThenBy(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.Title).ThenBy(ticket => ticket.Id),
            "status" => descending ? tickets.OrderByDescending(ticket => ticket.Status).ThenBy(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.Status).ThenBy(ticket => ticket.Id),
            "priority" => descending ? tickets.OrderByDescending(ticket => ticket.Priority).ThenBy(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.Priority).ThenBy(ticket => ticket.Id),
            "updatedat" => descending ? tickets.OrderByDescending(ticket => ticket.UpdatedAt).ThenBy(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.UpdatedAt).ThenBy(ticket => ticket.Id),
            _ => descending ? tickets.OrderByDescending(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id) : tickets.OrderBy(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id)
        };

        var items = await tickets.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(ticket => ToResponse(ticket)).ToListAsync(cancellationToken);
        return new PagedResult<TicketResponse>(items, query.Page, query.PageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }

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

    public async Task<IReadOnlyList<TicketCommentResponse>?> GetCommentsAsync(int ticketId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Tickets.AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)) return null;
        return await dbContext.TicketComments.AsNoTracking().Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new TicketCommentResponse(comment.Id, comment.TicketId, comment.AuthorId, comment.Body, comment.IsInternal, comment.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketCommentResponse?> AddCommentAsync(int ticketId, TicketCommentRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Tickets.AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)) return null;
        if (string.IsNullOrWhiteSpace(request.Body)) throw new TicketInputException("Comment body is required.");
        if (!await dbContext.Users.AnyAsync(user => user.Id == request.AuthorId && user.IsActive, cancellationToken))
            throw new TicketInputException($"Active author {request.AuthorId} does not exist.");
        var comment = new TicketComment
        {
            TicketId = ticketId,
            AuthorId = request.AuthorId,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.TicketComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TicketCommentResponse(comment.Id, comment.TicketId, comment.AuthorId, comment.Body, comment.IsInternal, comment.CreatedAt);
    }

    public async Task<TicketResponse?> AssignAsync(int ticketId, TicketAssignmentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        if (request.AssigneeId is not null && !await dbContext.Users.AnyAsync(user =>
                user.Id == request.AssigneeId && user.IsActive && (user.Role == UserRole.Agent || user.Role == UserRole.Admin), cancellationToken))
            throw new TicketInputException($"Active agent {request.AssigneeId} does not exist.");
        ticket.AssigneeId = request.AssigneeId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(ticket);
    }

    private static void ValidateQuery(TicketQuery query)
    {
        if (query.Page < 1 || query.Page > 100_000) throw new TicketInputException("Page must be between 1 and 100000.");
        if (query.PageSize is < 1 or > 100) throw new TicketInputException("PageSize must be between 1 and 100.");
        if (query.CategoryId is <= 0 || query.RequesterId is <= 0 || query.AssigneeId is <= 0)
            throw new TicketInputException("CategoryId, RequesterId and AssigneeId must be positive when supplied.");
        if (query.CreatedFrom is not null && query.CreatedTo is not null && query.CreatedFrom > query.CreatedTo)
            throw new TicketInputException("CreatedFrom must be earlier than or equal to CreatedTo.");
        if (query.Search?.Length > 200) throw new TicketInputException("Search cannot exceed 200 characters.");
        if (query.Status is not null && !Enum.IsDefined(query.Status.Value)) throw new TicketInputException("Status is not a supported ticket status.");
        if (query.Priority is not null && !Enum.IsDefined(query.Priority.Value)) throw new TicketInputException("Priority is not a supported ticket priority.");
        if (!new[] { "id", "title", "status", "priority", "createdat", "updatedat" }.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
            throw new TicketInputException("SortBy must be one of: id, title, status, priority, createdAt, updatedAt.");
        if (!query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) && !query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
            throw new TicketInputException("SortDirection must be 'asc' or 'desc'.");
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
