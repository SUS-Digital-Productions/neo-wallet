using NeoWallet.Backend.Services;
using SUS.EOS.EosioSigningRequest;
using SUS.EOS.Sharp.Services;

namespace NeoWallet.Backend;

public static class ServiceRegistration
{
    public static IServiceCollection AddBackendServices(this IServiceCollection services)
    {
        services.AddSingleton<IWalletStorageService, WalletStorageService>();
        services.AddSingleton<IWalletStateService, WalletStateService>();
        services.AddSingleton<IChainClientFactory, ChainClientFactory>();
        services.AddSingleton<IAntelopeTransactionService, AntelopeTransactionService>();
        services.AddEsrServices();
        return services;
    }
}
