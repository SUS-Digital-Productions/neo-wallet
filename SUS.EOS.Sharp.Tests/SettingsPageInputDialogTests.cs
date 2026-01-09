using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SUS.EOS.NeoWallet.Pages;
using SUS.EOS.NeoWallet.Pages.Components;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models.WalletData;
using SUS.EOS.NeoWallet.Services;
using Xunit;

namespace SUS.EOS.Sharp.Tests
{
    public class SettingsPageInputDialogTests
    {
        [Fact]
        public async Task ProcessTestEsrAsync_SetsKeyboard_And_HandlesCancel()
        {
            var original = InputDialog.ShowAsyncHandler;
            try
            {
                InputDialog.ShowAsyncHandler = (dlg, parent) =>
                {
                    // Page should set URL keyboard before showing
                    Assert.Equal(Keyboard.Url, dlg.EntryKeyboard);
                    return Task.FromResult<string?>(null);
                };

                var themeService = new ThemeService();
                var networkService = new FakeNetworkService();
                var storage = new FakeWalletStorageService();

                var page = new SettingsPage(themeService, networkService, storage);

                // Call internal method directly (made internal for testing)
                await page.ProcessTestEsrAsync();

                // If we reached here without exception, the dialog was created and ShowAsyncHandler ran
            }
            finally
            {
                InputDialog.ShowAsyncHandler = original;
            }
        }

        private sealed class FakeNetworkService : INetworkService
        {
            public Task<NetworkConfig?> GetDefaultNetworkAsync() => Task.FromResult<NetworkConfig?>(null);

            public Task<Dictionary<string, NetworkConfig>> GetNetworksAsync() => Task.FromResult(new Dictionary<string, NetworkConfig>());

            public Task SetDefaultNetworkAsync(string id)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeWalletStorageService : IWalletStorageService
        {
            public Task DeleteWalletAsync() => Task.CompletedTask;

            public Task SaveWalletAsync(object wallet) => Task.CompletedTask;

            public Task<object?> LoadWalletAsync() => Task.FromResult<object?>(null);
        }
    }
}
