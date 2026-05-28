namespace Thiccdal.Infrastructure.Bot;

public interface IProactiveMessagingService
{
    Task ExecuteDueMessages(CancellationToken cancellationToken = default);
}
