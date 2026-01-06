using Microsoft.Extensions.DependencyInjection;
using SUS.EOS.EosioSigningRequest.Services;

namespace SUS.EOS.EosioSigningRequest;

/// <summary>
/// Extension methods for registering ESR services with dependency injection
/// </summary>
public static class EsrServiceCollectionExtensions
{
    /// <summary>
    /// Add ESR services to the service collection
    /// Uses in-memory state store (non-persistent)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEsrServices(this IServiceCollection services)
    {
        return AddEsrServices(services, useMemoryStore: true);
    }

    /// <summary>
    /// Add ESR services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="useMemoryStore">Use in-memory state store (non-persistent). If false, you must register IEsrStateStore yourself</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEsrServices(
        this IServiceCollection services,
        bool useMemoryStore = true
    )
    {
        // Register HTTP client for ESR service
        services.AddHttpClient();

        // Register ESR service
        services.AddSingleton<IEsrService, EsrService>();

        // Register state store if memory store is requested
        if (useMemoryStore)
        {
            services.AddSingleton<IEsrStateStore, MemoryEsrStateStore>();
        }

        // Register ESR session manager as singleton
        services.AddSingleton<IEsrSessionManager, EsrSessionManager>();

        return services;
    }

    /// <summary>
    /// Add ESR services with custom state store
    /// </summary>
    /// <typeparam name="TStateStore">Custom state store implementation</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEsrServices<TStateStore>(this IServiceCollection services)
        where TStateStore : class, IEsrStateStore
    {
        services.AddHttpClient();
        services.AddSingleton<IEsrService, EsrService>();
        services.AddSingleton<IEsrStateStore, TStateStore>();
        services.AddSingleton<IEsrSessionManager, EsrSessionManager>();

        return services;
    }

    /// <summary>
    /// Add ESR services with custom state store factory
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="stateStoreFactory">Factory function to create state store</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEsrServices(
        this IServiceCollection services,
        Func<IServiceProvider, IEsrStateStore> stateStoreFactory
    )
    {
        ArgumentNullException.ThrowIfNull(stateStoreFactory);

        services.AddHttpClient();
        services.AddSingleton<IEsrService, EsrService>();
        services.AddSingleton(stateStoreFactory);
        services.AddSingleton<IEsrSessionManager, EsrSessionManager>();

        return services;
    }
}
