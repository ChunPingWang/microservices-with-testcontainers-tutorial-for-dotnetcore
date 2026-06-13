namespace SharedKernel.Domain.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    string EventType => GetType().Name;
}
