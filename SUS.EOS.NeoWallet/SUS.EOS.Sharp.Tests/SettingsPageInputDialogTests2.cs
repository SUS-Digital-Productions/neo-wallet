using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using SUS.EOS.NeoWallet.Pages;
using SUS.EOS.NeoWallet.Pages.Components;
using SUS.EOS.NeoWallet.Services.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;
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
            public Task AddNetworkAsync(string networkId, SUS.EOS.NeoWallet.Services.Models.NetworkConfig config) => Task.CompletedTask;

            public Task<bool> RemoveNetworkAsync(string networkId) => Task.FromResult(false);

            public Task<Dictionary<string, SUS.EOS.NeoWallet.Services.Models.NetworkConfig>> GetNetworksAsync() => Task.FromResult(new Dictionary<string, SUS.EOS.NeoWallet.Services.Models.NetworkConfig>());

            public Task<SUS.EOS.NeoWallet.Services.Models.NetworkConfig?> GetNetworkAsync(string networkId) => Task.FromResult<SUS.EOS.NeoWallet.Services.Models.NetworkConfig?>(null);

            public Task SetDefaultNetworkAsync(string networkId) => Task.CompletedTask;

            public Task<SUS.EOS.NeoWallet.Services.Models.NetworkConfig?> GetDefaultNetworkAsync() => Task.FromResult<SUS.EOS.NeoWallet.Services.Models.NetworkConfig?>(null);

            public Task<bool> TestNetworkAsync(string networkId) => Task.FromResult(true);

            public Task InitializePredefinedNetworksAsync() => Task.CompletedTask;
        }

        private sealed class FakeWalletStorageService : IWalletStorageService
        {
            public Task<WalletData?> LoadWalletAsync() => Task.FromResult<WalletData?>(null);

            public Task SaveWalletAsync(WalletData walletData) => Task.CompletedTask;

            public Task<WalletData> CreateWalletAsync(string password, string? description = null) => Task.FromResult(new WalletData());

            public Task<bool> ValidatePasswordAsync(string password) => Task.FromResult(true);

            public Task<bool> ChangePasswordAsync(string currentPassword, string newPassword) => Task.FromResult(true);

            public Task<bool> WalletExistsAsync() => Task.FromResult(false);

            public Task DeleteWalletAsync() => Task.CompletedTask;

            public Task<string> ExportWalletAsync(string password) => Task.FromResult(string.Empty);

            public Task<WalletData> ImportWalletAsync(string backupData, string password) => Task.FromResult(new WalletData());

            public void LockWallet() { }

            public Task<bool> UnlockWalletAsync(string password) => Task.FromResult(true);

            public bool IsUnlocked => false;

            public string? GetUnlockedPrivateKey(string publicKey) => null;

            public Task<bool> AddKeyToStorageAsync(string privateKey, string publicKey, string password, string? label = null) => Task.FromResult(true);
        }
    }
}
