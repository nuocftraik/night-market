using NightMarket.WebApi.Application.Common.Events;
using NightMarket.WebApi.Domain.Common.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace NightMarket.WebApi.Infrastructure.Common.Events;

/// <summary>
/// Implementation của IEventPublisher sử dụng MediatR
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IPublisher _mediator;

    public EventPublisher(ILogger<EventPublisher> logger, IPublisher mediator) =>
        (_logger, _mediator) = (logger, mediator);

    /// <summary>
    /// Wrap domain event thành MediatR notification và publish
    /// </summary>
    public Task PublishAsync(IEvent @event)
    {
        _logger.LogInformation("Publishing Event: {event}", @event.GetType().Name);
        return _mediator.Publish(CreateEventNotification(@event));
    }

    /// <summary>
    /// Dynamically create EventNotification&lt;TEvent&gt; via reflection
    /// </summary>
    private INotification CreateEventNotification(IEvent @event)
    {
        return (INotification)Activator.CreateInstance(
            typeof(EventNotification<>).MakeGenericType(@event.GetType()), @event)!;
    }
}
