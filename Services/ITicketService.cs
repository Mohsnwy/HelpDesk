using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketResponse>> GetAllAsync(TicketQuery query, int? requesterId, CancellationToken cancellationToken);
    Task<TicketResponse?> GetByIdAsync(int id, int? requesterId, CancellationToken cancellationToken);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, int requesterId, CancellationToken cancellationToken);
    Task<(TicketResponse? Ticket, bool InvalidTransition)> UpdateAsync(int id, UpdateTicketRequest request, int? requesterId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketCommentResponse>?> GetCommentsAsync(int ticketId, int? requesterId, bool includeInternal, CancellationToken cancellationToken);
    Task<TicketCommentResponse?> AddCommentAsync(int ticketId, int authorId, int? requesterId, TicketCommentRequest request, CancellationToken cancellationToken);
    Task<TicketResponse?> AssignAsync(int ticketId, TicketAssignmentRequest request, CancellationToken cancellationToken);
}
