using NightMarket.WebApi.Application.Common.Interfaces;
using NightMarket.WebApi.Domain.Common.Contracts;

namespace NightMarket.WebApi.Application.Common.Events;

/// <summary>
/// Interface để publish domain events
/// Implementation sẽ dùng MediatR để dispatch events đến handlers
/// </summary>
public interface IEventPublisher : ITransientService
{
    /// <summary>
    /// Publish domain event
    /// </summary>
    Task PublishAsync(IEvent @event);
}
