using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketResponse>> GetAllAsync(TicketQuery query, CancellationToken cancellationToken);
    Task<TicketResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);
    Task<(TicketResponse? Ticket, bool InvalidTransition)> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketCommentResponse>?> GetCommentsAsync(int ticketId, CancellationToken cancellationToken);
    Task<TicketCommentResponse?> AddCommentAsync(int ticketId, TicketCommentRequest request, CancellationToken cancellationToken);
    Task<TicketResponse?> AssignAsync(int ticketId, TicketAssignmentRequest request, CancellationToken cancellationToken);
}
