using SharedKernel.Domain.Events;

namespace SharedKernel.Domain.Ports;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent;
}
