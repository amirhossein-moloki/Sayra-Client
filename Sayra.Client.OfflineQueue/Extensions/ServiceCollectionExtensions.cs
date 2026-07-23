using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Security;
using Sayra.Client.OfflineQueue.Serialization;

namespace Sayra.Client.OfflineQueue.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOfflineQueue(this IServiceCollection services)
    {
        services.AddSingleton<IEventSerializer, EventSerializer>();
        services.AddSingleton<IQueueSecurityManager, QueueSecurityManager>();
        services.AddSingleton<IOfflineQueueManager, OfflineQueueManager>();
        return services;
    }
}
