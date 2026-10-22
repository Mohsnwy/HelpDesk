namespace HelpDesk.Api.Services;

public sealed class TicketInputException(string message) : Exception(message);
