using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public interface ITicketService
{
    Task<IReadOnlyList<TicketResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<TicketResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken);
    Task<(TicketResponse? Ticket, bool InvalidTransition)> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
