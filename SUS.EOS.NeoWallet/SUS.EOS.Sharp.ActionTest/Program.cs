using System;
using System.Threading.Tasks;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Services;

namespace SUS.EOS.Sharp.ActionTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  SUS.EOS.Sharp - addbost Action Test");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            // Prompt for private key
            Console.Write("Enter your private key (WIF or PVT_K1_ format): ");
            var privateKeyInput = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(privateKeyInput))
            {
                Console.WriteLine("❌ Private key is required!");
                return;
            }

            // Parse private key
            var key = new EosioKey(privateKeyInput);
            Console.WriteLine($"✓ Private key loaded");
            Console.WriteLine($"  Public Key: {key.PublicKey}\n");

            // Prompt for wallet name
            Console.Write("Enter wallet name (account): ");
            var walletName = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(walletName))
            {
                Console.WriteLine("❌ Wallet name is required!");
                return;
            }

            // Prompt for credits
            Console.Write("Enter credits (uint64): ");
            var creditsInput = Console.ReadLine();
            
            if (!ulong.TryParse(creditsInput, out var credits))
            {
                Console.WriteLine("❌ Invalid credits value!");
                return;
            }

            // Prompt for blockchain endpoint
            Console.Write("\nEnter blockchain endpoint (default: https://wax.greymass.com): ");
            var endpoint = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://wax.greymass.com";
            }

            Console.WriteLine($"\n📡 Connecting to {endpoint}...");

            // Create blockchain client
            using var client = new AntelopeHttpClient(endpoint);
            
            // Get chain info
            var chainInfo = await client.GetInfoAsync();
            Console.WriteLine($"✓ Connected to chain: {chainInfo.ChainId}");
            Console.WriteLine($"  Head Block: #{chainInfo.HeadBlockNum}");
            Console.WriteLine($"  Block Time: {chainInfo.HeadBlockTime}\n");

            // Create transaction service
            var transactionService = new AntelopeTransactionService(client);

            // Build action data
            var actionData = new
            {
                wallet = walletName,
                credits = credits
            };

            Console.WriteLine("📝 Building transaction...");
            Console.WriteLine($"  Contract: testingpoint");
            Console.WriteLine($"  Action: addbost");
            Console.WriteLine($"  Wallet: {walletName}");
            Console.WriteLine($"  Credits: {credits}\n");

            // Build and sign transaction
            var signedTx = await transactionService.BuildAndSignWithAbiAsync(
                client: client,
                chainInfo: chainInfo,
                actor: "testingpoint",
                privateKeyWif: privateKeyInput,
                contract: "testingpoint",
                action: "addbost",
                data: actionData,
                authority: "active"
            );

            Console.WriteLine($"✓ Transaction signed");
            Console.WriteLine($"  Signatures: {signedTx.Signatures.Count}");
            Console.WriteLine($"  Packed size: {signedTx.PackedTransaction.Length / 2} bytes\n");

            // Ask for confirmation
            Console.Write("⚠️  Push transaction to blockchain? (yes/no): ");
            var confirm = Console.ReadLine();

            if (confirm?.ToLower() == "yes" || confirm?.ToLower() == "y")
            {
                Console.WriteLine("\n📤 Pushing transaction to blockchain...");
                
                var pushResult = await client.PushTransactionAsync(new
                {
                    signatures = signedTx.Signatures,
                    compression = 0,
                    packed_context_free_data = "",
                    packed_trx = signedTx.PackedTransaction
                });

                Console.WriteLine("\n✅ Transaction successful!");
                Console.WriteLine($"  Transaction ID: {pushResult.TransactionId}");
                Console.WriteLine($"  Block: #{pushResult.Processed.BlockNum}");
                Console.WriteLine($"  Block Time: {pushResult.Processed.BlockTime}");
                
                if (endpoint.Contains("wax"))
                {
                    Console.WriteLine($"\n🔗 View on explorer:");
                    Console.WriteLine($"  https://waxblock.io/transaction/{pushResult.TransactionId}");
                }
            }
            else
            {
                Console.WriteLine("\n⊘ Transaction not pushed to blockchain");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Details: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }

        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
