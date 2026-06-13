using MassTransit;
using SharedKernel.Domain.Events;
using SharedKernel.Domain.Ports;

namespace SharedKernel.Infrastructure.Messaging;

public sealed class MassTransitEventPublisher(IPublishEndpoint endpoint) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent
        => endpoint.Publish(integrationEvent, ct);
}
