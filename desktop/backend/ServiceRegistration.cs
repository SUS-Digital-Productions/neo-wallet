using NeoWallet.Backend.Services;
using SUS.EOS.EosioSigningRequest;
using SUS.EOS.EosioSigningRequest.Services;
using SUS.EOS.Sharp.Services;

namespace NeoWallet.Backend;

public static class ServiceRegistration
{
    public static IServiceCollection AddBackendServices(this IServiceCollection services)
    {
        services.AddSingleton<BackendTokenHolder>();
        services.AddSingleton<IWalletStorageService, WalletStorageService>();
        services.AddSingleton<IWalletStateService, WalletStateService>();
        services.AddSingleton<IChainClientFactory, ChainClientFactory>();
        services.AddSingleton<IAntelopeTransactionService, AntelopeTransactionService>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<AutoLockService>();
        services.AddHostedService(sp => sp.GetRequiredService<AutoLockService>());
        // Register file-backed ESR state store so link ID and sessions persist across restarts
        services.AddSingleton<IEsrStateStore, FileEsrStateStore>();
        services.AddEsrServices(useMemoryStore: false);
        services.AddSingleton<EsrListenerService>();
        services.AddHostedService(sp => sp.GetRequiredService<EsrListenerService>());
        return services;
    }
}
