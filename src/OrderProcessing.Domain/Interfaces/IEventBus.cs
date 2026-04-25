using OrderProcessing.Domain.Events;

namespace OrderProcessing.Domain.Interfaces;

/// <summary>
/// In-process event bus for publishing and handling domain events.
/// Supports async event handling for the order processing pipeline.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : DomainEvent;
}