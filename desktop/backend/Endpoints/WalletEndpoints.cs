using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        app.MapGet("/api/wallet/summary", (IWalletStateService wallet, EsrListenerService listener, AutoLockService autoLock) =>
        {
            string? lockExpiresAt = null;
            if (wallet.WalletUnlocked && autoLock.TimeoutMinutes > 0)
            {
                var expires = autoLock.LastActivity.AddMinutes(autoLock.TimeoutMinutes);
                lockExpiresAt = expires.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            }

            return Results.Ok(new WalletSummaryDto(
                ActiveNetwork: wallet.ActiveNetwork,
                ActiveAccount: wallet.ActiveAccount,
                ListenerStatus: listener.Status.ToString(),
                AutoLockMinutes: autoLock.TimeoutMinutes,
                LockExpiresAt: lockExpiresAt,
                WalletUnlocked: wallet.WalletUnlocked
            ));
        });

        app.MapPost("/api/wallet/create", (CreateWalletRequest req, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            if (wallet.WalletLoaded)
                return Results.Problem("Wallet already exists.", statusCode: StatusCodes.Status409Conflict);
            var ok = wallet.CreateWallet(req.Password);
            return ok ? Results.Ok(new UnlockResponse(true, tokenHolder.Token)) : Results.Problem("Failed to create wallet.");
        });

        app.MapPost("/api/wallet/unlock", (UnlockRequest req, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            var ok = wallet.Unlock(req.Password);
            return Results.Ok(new UnlockResponse(ok, ok ? tokenHolder.Token : null));
        });

        app.MapPost("/api/wallet/lock", (IWalletStateService wallet) =>
        {
            wallet.Lock();
            return Results.Ok();
        });

        app.MapGet("/api/wallet/export", (IWalletStorageService storage, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);
            if (!storage.WalletFileExists)
                return Results.Problem("No wallet file found.", statusCode: StatusCodes.Status404NotFound);

            var bytes = storage.ReadRawFile();
            if (bytes is null)
                return Results.Problem("Failed to read wallet file.", statusCode: StatusCodes.Status500InternalServerError);

            return Results.File(bytes, "application/octet-stream", "wallet.json");
        });

        app.MapPost("/api/wallet/import", (ImportWalletRequest req, IWalletStorageService storage, IWalletStateService wallet, BackendTokenHolder tokenHolder) =>
        {
            byte[] fileBytes;
            try { fileBytes = Convert.FromBase64String(req.FileBase64); }
            catch { return Results.Problem("Invalid file data.", statusCode: StatusCodes.Status400BadRequest); }

            // Validate the candidate file BEFORE overwriting the existing wallet.
            // ValidateAndReplace decrypts in-memory, then atomically swaps with backup.
            var data = storage.ValidateAndReplace(fileBytes, req.Password);
            if (data is null)
            {
                return Results.Problem(
                    "Wrong password, or file is not a NeoWallet-format wallet. " +
                    "If this is an Anchor / scatter backup, use the dedicated Anchor import.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Wallet replaced — re-prime the in-memory state with the new password
            wallet.Unlock(req.Password);
            return Results.Ok(new UnlockResponse(true, tokenHolder.Token));
        });

        app.MapPost("/api/wallet/import-anchor", (ImportAnchorWalletRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem(
                    "Wallet must be unlocked to import keys from an Anchor backup. " +
                    "Create or unlock your NeoWallet first, then import.",
                    statusCode: StatusCodes.Status403Forbidden);

            byte[] fileBytes;
            try { fileBytes = Convert.FromBase64String(req.FileBase64); }
            catch { return Results.Problem("Invalid file data.", statusCode: StatusCodes.Status400BadRequest); }

            var result = AnchorWalletImporter.TryImport(fileBytes, req.Password);
            if (result is null || result.PrivateKeysWif.Count == 0)
            {
                return Results.Problem(
                    "Could not decrypt the Anchor wallet file with the given password. " +
                    "If you are sure the password is correct, the file format may not be supported yet.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var publicKeys = new List<string>();
            foreach (var wif in result.PrivateKeysWif)
            {
                try
                {
                    var added = wallet.AddKey(wif, label: $"Imported from Anchor ({result.Format})");
                    publicKeys.Add(added.PublicKey);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[ANCHOR-IMPORT] Failed to add key: {ex.Message}");
                }
            }

            return Results.Ok(new ImportAnchorWalletResponse(
                ImportedKeys: publicKeys.Count,
                PublicKeys: publicKeys.ToArray(),
                Format: result.Format));
        });

        app.MapPost("/api/accounts/private-key", (GetPrivateKeyRequest req, IWalletStateService wallet) =>
        {
            if (!wallet.WalletUnlocked)
                return Results.Problem("Wallet is locked.", statusCode: StatusCodes.Status403Forbidden);

            var wif = wallet.GetPrivateKeyWif(req.Account, req.Authority, req.ChainId);
            if (string.IsNullOrEmpty(wif))
                return Results.Problem("Account not found.", statusCode: StatusCodes.Status404NotFound);

            return Results.Ok(new GetPrivateKeyResponse(wif));
        });
    }
}
