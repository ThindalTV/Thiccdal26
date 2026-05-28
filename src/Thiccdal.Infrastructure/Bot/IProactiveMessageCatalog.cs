namespace Thiccdal.Infrastructure.Bot;

public interface IProactiveMessageCatalog
{
    Task<IReadOnlyList<ProactiveMessageDefinition>> GetEnabledMessages(CancellationToken cancellationToken = default);

    Task MarkSent(long messageId, DateTimeOffset sentAt, CancellationToken cancellationToken = default);
}
