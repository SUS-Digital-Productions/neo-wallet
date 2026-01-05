using System.Text.Json;
using SUS.EOS.Sharp.Tests;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("    SUS.EOS.Sharp - WAX Blockchain Test Application");
Console.WriteLine("    With Full Cryptographic Support");
Console.WriteLine("═══════════════════════════════════════════════════════════\n");

// Configuration
const string WaxEndpoint = "https://api.wax.alohaeos.com"; // Try different endpoint
const string TestAccount = "testingpoint";
const string TestContract = "testingpoint"; // Replace with actual contract

Console.WriteLine("Configuration:");
Console.WriteLine($"  Endpoint: {WaxEndpoint}");
Console.WriteLine($"  Account: {TestAccount}");
Console.WriteLine($"  Contract: {TestContract}\n");

using var client = new WaxBlockchainClient(WaxEndpoint);

try
{
    // Test 1: Get Chain Info
    Console.WriteLine("📡 Test 1: Getting Chain Information...");
    Console.WriteLine("─────────────────────────────────────────────────────────");
    var chainInfo = await client.GetInfoAsync();
    Console.WriteLine($"✓ Chain ID: {chainInfo.ChainId}");
    Console.WriteLine($"✓ Server Version: {chainInfo.ServerVersion}");
    Console.WriteLine($"✓ Head Block: #{chainInfo.HeadBlockNum}");
    Console.WriteLine($"✓ Last Irreversible Block: #{chainInfo.LastIrreversibleBlockNum}");
    Console.WriteLine($"✓ Head Block Time: {chainInfo.HeadBlockTime:yyyy-MM-dd HH:mm:ss} UTC");
    Console.WriteLine($"✓ Producer: {chainInfo.HeadBlockProducer}\n");

    // Test 2: Get Account Information
    Console.WriteLine($"👤 Test 2: Getting Account Information ({TestAccount})...");
    Console.WriteLine("─────────────────────────────────────────────────────────");
    var account = await client.GetAccountAsync(TestAccount);
    Console.WriteLine($"✓ Account Name: {account.AccountName}");
    Console.WriteLine($"✓ Created: {account.Created:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine(
        $"✓ RAM: {account.RamUsage:N0} / {account.RamQuota:N0} bytes ({(double)account.RamUsage / account.RamQuota * 100:F2}%)"
    );
    Console.WriteLine($"✓ CPU: {account.CpuLimit.Used:N0} / {account.CpuLimit.Max:N0} μs");
    Console.WriteLine($"✓ NET: {account.NetLimit.Used:N0} / {account.NetLimit.Max:N0} bytes\n");

    // Test 3: Get Token Balances
    Console.WriteLine($"💰 Test 3: Getting Token Balances ({TestAccount})...");
    Console.WriteLine("─────────────────────────────────────────────────────────");

    // WAX balance
    var waxBalances = await client.GetCurrencyBalanceAsync("eosio.token", TestAccount, "WAX");
    if (waxBalances.Count > 0)
    {
        Console.WriteLine($"✓ WAX Balance: {waxBalances[0]}");
    }
    else
    {
        Console.WriteLine("✓ WAX Balance: 0.00000000 WAX");
    }

    // Try to get other token balances
    try
    {
        var allBalances = await client.GetCurrencyBalanceAsync("eosio.token", TestAccount);
        if (allBalances.Count > 0)
        {
            Console.WriteLine($"✓ All Balances: {string.Join(", ", allBalances)}");
        }
    }
    catch
    {
        // Ignore if no other balances
    }
    Console.WriteLine();

    // Test 4: Transaction Signing with Library
    Console.WriteLine("🔐 Test 4: Transaction Building & Signing (Library Implementation)");
    Console.WriteLine("─────────────────────────────────────────────────────────");
    Console.WriteLine("Enter your WAX private key to sign transaction (or press Enter to skip):");
    Console.WriteLine("⚠️  Security Warning: Never share your private key!");
    Console.WriteLine("Format: 5... (legacy WIF) or PVT_K1_... (modern format)");
    Console.Write("\nPrivate Key: ");
    var privateKey = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(privateKey))
    {
        try
        {
            // Debug: Show time values
            Console.WriteLine($"\n⏰ Time Debug:");
            Console.WriteLine(
                $"  HeadBlockTime from API: {chainInfo.HeadBlockTime:yyyy-MM-dd HH:mm:ss} (Kind: {chainInfo.HeadBlockTime.Kind})"
            );
            Console.WriteLine(
                $"  HeadBlockTime UTC:      {chainInfo.HeadBlockTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss}"
            );
            Console.WriteLine($"  Current Local Time:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Current UTC Time:       {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

            Console.WriteLine("\n🔑 Deriving public key from private key...");
            var publicKey = WaxTransactionHelper.GetPublicKey(privateKey);
            Console.WriteLine($"✓ Public Key: {publicKey}");

            // Calculate ref_block_prefix from the head block ID
            var refBlockPrefix = WaxBlockchainClient.CalculateRefBlockPrefix(chainInfo.HeadBlockId);
            Console.WriteLine($"✓ Using Head Block #{chainInfo.HeadBlockNum}");
            Console.WriteLine($"✓ Block ID: {chainInfo.HeadBlockId.Substring(0, 16)}...");
            Console.WriteLine($"✓ RefBlockPrefix: {refBlockPrefix}");

            // Use ABI-based serialization - the library handles everything!
            Console.WriteLine("\n🔧 Using ABI-based serialization (automatic)...");
            Console.WriteLine("  Fetching contract ABI from chain...");

            // Define action data as a simple object - ABI serializer handles the rest
            var actionData = new { wallet = "liqbu.wam", credits = 1000UL };

            Console.WriteLine(
                $"  Action data: {{ wallet: \"{actionData.wallet}\", credits: {actionData.credits} }}"
            );

            Console.WriteLine(
                "\n📝 Building and signing transaction using SUS.EOS.Sharp library with ABI..."
            );

            var signedTx = await WaxTransactionHelper.BuildAndSignWithAbiAsync(
                client,
                chainInfo,
                refBlockPrefix,
                TestAccount,
                privateKey,
                TestContract,
                "addboost",
                actionData,
                TimeSpan.FromMinutes(1)
            );

            Console.WriteLine($"✓ Transaction signed successfully!");
            Console.WriteLine($"✓ Signature: {signedTx.Signatures[0]}");
            Console.WriteLine($"✓ Packed Transaction: {signedTx.PackedTransaction}");
            Console.WriteLine($"✓ Packed size: {signedTx.PackedTransaction.Length / 2} bytes");

            // Display transaction JSON
            var txJson = JsonSerializer.Serialize(
                signedTx.Transaction,
                new JsonSerializerOptions { WriteIndented = true }
            );
            Console.WriteLine($"\n📄 Transaction Details:");
            Console.WriteLine(txJson);

            // Ask if user wants to push transaction
            Console.WriteLine("\n⚠️  Push transaction to blockchain? (yes/no): ");
            var confirm = Console.ReadLine();

            if (confirm?.ToLower() == "yes" || confirm?.ToLower() == "y")
            {
                Console.WriteLine("\n📤 Pushing transaction to blockchain...");
                var pushRequest = new WaxPushTransactionRequest
                {
                    Signatures = signedTx.Signatures,
                    Compression = 0,
                    PackedContextFreeData = "",
                    PackedTrx = signedTx.PackedTransaction,
                };

                var result = await client.PushTransactionAsync(pushRequest);
                Console.WriteLine($"\n✅ Transaction successful!");
                Console.WriteLine($"Transaction ID: {result.TransactionId}");
                Console.WriteLine($"Block: #{result.Processed.BlockNum}");
                Console.WriteLine($"Block Time: {result.Processed.BlockTime}");
                Console.WriteLine($"\n🔗 View on explorer:");
                Console.WriteLine($"https://waxblock.io/transaction/{result.TransactionId}");
            }
            else
            {
                Console.WriteLine("\n⊘ Transaction not pushed to blockchain");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Signing error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Details: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack trace: {ex.StackTrace}");
        }
    }
    else
    {
        Console.WriteLine("⊘ Skipped transaction signing\n");
    }

    // Summary
    Console.WriteLine("\n═══════════════════════════════════════════════════════════");
    Console.WriteLine("✓ All tests completed successfully!");
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine("\n✅ Implemented Features (in SUS.EOS.Sharp Library):");
    Console.WriteLine("  ✓ EOSIO secp256k1 cryptographic signing (BouncyCastle)");
    Console.WriteLine("  ✓ Private key handling (WIF, PVT_K1_, hex formats)");
    Console.WriteLine("  ✓ Public key derivation");
    Console.WriteLine("  ✓ Transaction builder pattern");
    Console.WriteLine("  ✓ ABI-based automatic binary serialization");
    Console.WriteLine("  ✓ Signature encoding (SIG_K1_ format with RIPEMD160)");
    Console.WriteLine("  ✓ Transaction pushing to blockchain");
    Console.WriteLine();
    Console.WriteLine("📦 Library Structure:");
    Console.WriteLine("  - SUS.EOS.Sharp.Cryptography.EosioKey - Key management");
    Console.WriteLine("  - SUS.EOS.Sharp.Models.AbiDefinition - ABI models");
    Console.WriteLine("  - SUS.EOS.Sharp.Serialization.AbiSerializer - ABI-based encoding");
    Console.WriteLine("  - SUS.EOS.Sharp.Serialization.EosioSerializer - Transaction encoding");
    Console.WriteLine("  - SUS.EOS.Sharp.Transactions.EosioTransactionBuilder - Builder pattern");
    Console.WriteLine("  - SUS.EOS.Sharp.Signatures.EosioSignatureProvider - Signing");
    Console.WriteLine("  - SUS.EOS.Sharp.Services.AntelopeTransactionService - High-level API");
    Console.WriteLine();
    Console.WriteLine("📋 Usage Notes:");
    Console.WriteLine("  - Keep your private key secure (use environment variables)");
    Console.WriteLine("  - Test on testnet before mainnet");
    Console.WriteLine("  - ABI is fetched automatically from the blockchain");
    Console.WriteLine();
    Console.WriteLine("🔗 Resources:");
    Console.WriteLine($"  - Account Explorer: https://waxblock.io/account/{TestAccount}");
    Console.WriteLine(
        $"  - Action Details: https://waxblock.io/account/{TestAccount}?action=addboost"
    );
    Console.WriteLine("  - WAX Developer Docs: https://developer.wax.io/");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"\n❌ HTTP Error: {ex.Message}");
    Console.WriteLine("Make sure you have internet connection and the WAX endpoint is accessible.");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
