using Microsoft.Extensions.Logging;
using SUS.EOS.NeoWallet.Pages;
using SUS.EOS.NeoWallet.Services;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.Sharp.ESR;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.NeoWallet
{
    public static class MauiProgramExtensions
    {
        public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder)
        {
            builder
                .UseMauiApp<App>(sp => new App(sp))
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register core services
            builder.Services.AddSingleton<ICryptographyService, CryptographyService>();
            builder.Services.AddSingleton<IWalletStorageService, WalletStorageService>();
            builder.Services.AddSingleton<IWalletAccountService, WalletAccountService>();
            builder.Services.AddSingleton<INetworkService, NetworkService>();
            builder.Services.AddSingleton<IWalletContextService, WalletContextService>();
            builder.Services.AddSingleton<IEsrService, EsrService>();
            builder.Services.AddSingleton<IAnchorCallbackService, AnchorCallbackService>();
            builder.Services.AddSingleton<IPriceFeedService, PriceFeedService>();
            builder.Services.AddSingleton<IEsrSessionManager, EsrSessionManager>();

            // Register blockchain services
            // Use a default endpoint - can be changed later via SetEndpoint
            builder.Services.AddSingleton<IAntelopeBlockchainClient>(sp =>
            {
                // Use default WAX endpoint - pages should update this based on selected network
                return new AntelopeHttpClient("https://api.wax.alohaeos.com");
            });

            builder.Services.AddTransient<
                IAntelopeTransactionService,
                AntelopeTransactionService
            >();
            builder.Services.AddTransient<
                IBlockchainOperationsService,
                BlockchainOperationsService
            >();

            // Register HttpClient for callback service
            builder.Services.AddHttpClient();

            // Theme service
            builder.Services.AddSingleton<ThemeService>();

            // Register pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<SendPage>();
            builder.Services.AddTransient<ReceivePage>();
            builder.Services.AddTransient<CreateAccountPage>();
            builder.Services.AddTransient<ImportWalletPage>();
            builder.Services.AddTransient<RecoverAccountPage>();
            builder.Services.AddTransient<WalletSetupPage>();
            builder.Services.AddTransient<EnterPasswordPage>();
            builder.Services.AddTransient<InitializePage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<ContractTablesPage>();
            builder.Services.AddTransient<ContractActionsPage>();
            builder.Services.AddTransient<EsrSigningPopupPage>();
            
            // Register platform-specific preferences (MAUI's default implementation)
            builder.Services.AddSingleton<IPreferences>(Microsoft.Maui.Storage.Preferences.Default);

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder;
        }
    }
}
