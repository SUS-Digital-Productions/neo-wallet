using SUS.EOS.NeoWallet.Pages;

namespace SUS.EOS.NeoWallet
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            System.Diagnostics.Trace.WriteLine("[APPSHELL] Constructor called");
            InitializeComponent();
            System.Diagnostics.Trace.WriteLine("[APPSHELL] InitializeComponent completed");
            
            // Register all routes for navigation
            System.Diagnostics.Trace.WriteLine("[APPSHELL] Registering routes");
            Routing.RegisterRoute("MainPage", typeof(MainPage));
            Routing.RegisterRoute("DashboardPage", typeof(DashboardPage));
            Routing.RegisterRoute("SendPage", typeof(SendPage));
            Routing.RegisterRoute("ReceivePage", typeof(ReceivePage));
            Routing.RegisterRoute("CreateAccountPage", typeof(CreateAccountPage));
            Routing.RegisterRoute("ImportWalletPage", typeof(ImportWalletPage));
            Routing.RegisterRoute("RecoverAccountPage", typeof(RecoverAccountPage));
            Routing.RegisterRoute("ContractTablesPage", typeof(ContractTablesPage));
            Routing.RegisterRoute("ContractActionsPage", typeof(ContractActionsPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("InitializePage", typeof(InitializePage));
            Routing.RegisterRoute("WalletSetupPage", typeof(WalletSetupPage));
            Routing.RegisterRoute("EnterPasswordPage", typeof(EnterPasswordPage));
            System.Diagnostics.Trace.WriteLine("[APPSHELL] All routes registered");
        }
    }
}
