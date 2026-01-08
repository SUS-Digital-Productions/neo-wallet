using System.Text.Json;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Services;

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("    SUS.EOS.Sharp - Add Boost Action Test");
Console.WriteLine("    testingpoint::addboost(wallet, credits)");
Console.WriteLine("═══════════════════════════════════════════════════════════\n");

// Configuration
const string WaxEndpoint = "https://api.wax.alohaeos.com";
const string TestAccount = "testingpoint";
const string TestContract = "testingpoint";

Console.WriteLine("Configuration:");
Console.WriteLine($"  Endpoint: {WaxEndpoint}");
Console.WriteLine($"  Account: {TestAccount}");
Console.WriteLine($"  Contract: {TestContract}\n");

// Prompt for action parameters
Console.Write("Enter wallet name: ");
var walletName = Console.ReadLine() ?? "";

Console.Write("Enter credits (uint64): ");
var creditsInput = Console.ReadLine() ?? "0";
if (!ulong.TryParse(creditsInput, out var credits))
{
    Console.WriteLine("❌ Invalid credits value. Must be a valid uint64.");
    return;
}

// Prompt for private key
Console.WriteLine("\n🔐 Enter your WAX private key to sign transaction:");
Console.WriteLine("⚠️  Security Warning: Never share your private key!");
Console.WriteLine("Format: 5... (legacy WIF) or PVT_K1_... (modern format)");
Console.Write("\nPrivate Key: ");
var privateKey = Console.ReadLine();

if (string.IsNullOrWhiteSpace(privateKey))
{
    Console.WriteLine("❌ Private key is required.");
    return;
}

using var client = new AntelopeHttpClient(WaxEndpoint);

try
{
    // Get chain info
    Console.WriteLine("\n📡 Getting chain information...");
    var chainInfo = await client.GetInfoAsync();
    Console.WriteLine($"✓ Chain ID: {chainInfo.ChainId}");
    Console.WriteLine($"✓ Head Block: #{chainInfo.HeadBlockNum}");

    // Derive public key
    Console.WriteLine("\n🔑 Deriving public key from private key...");
    var key = EosioKey.FromPrivateKey(privateKey);
    Console.WriteLine($"✓ Public Key: {key.PublicKey}");

    // Build action data
    var actionData = new Dictionary<string, object>
    {
        ["wallet"] = walletName,
        ["credits"] = credits,
    };

    Console.WriteLine("\n📝 Building transaction...");
    Console.WriteLine($"  Action: {TestContract}::addboost");
    Console.WriteLine($"  Parameters:");
    Console.WriteLine($"    - wallet: {walletName}");
    Console.WriteLine($"    - credits: {credits}");

    // Build and sign transaction
    var transactionService = new AntelopeTransactionService();

    var signedTx = await transactionService.BuildAndSignWithAbiAsync(
        client: client,
        chainInfo: chainInfo,
        actor: TestAccount,
        privateKeyWif: privateKey,
        contract: TestContract,
        action: "addboost",
        data: actionData,
        authority: "active"
    );

    Console.WriteLine($"✓ Transaction built and signed");
    Console.WriteLine($"✓ Signatures: {signedTx.Signatures.Count}");
    Console.WriteLine($"✓ Packed size: {signedTx.PackedTransaction.Length / 2} bytes");

    // Display transaction JSON
    var txJson = JsonSerializer.Serialize(
        signedTx.Transaction,
        new JsonSerializerOptions { WriteIndented = true }
    );
    Console.WriteLine($"\n📄 Transaction Details:");
    Console.WriteLine(txJson);

    // Ask for confirmation
    Console.WriteLine("\n⚠️  Push transaction to blockchain? (yes/no): ");
    var confirm = Console.ReadLine();

    if (confirm?.ToLower() == "yes" || confirm?.ToLower() == "y")
    {
        Console.WriteLine("\n📤 Pushing transaction to blockchain...");

        var pushRequest = new
        {
            signatures = signedTx.Signatures,
            compression = 0,
            packed_context_free_data = "",
            packed_trx = signedTx.PackedTransaction,
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
    Console.WriteLine($"\n❌ Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Details: {ex.InnerException.Message}");
    }
    Console.WriteLine($"\nStack trace: {ex.StackTrace}");
}

Console.WriteLine("\n═══════════════════════════════════════════════════════════");
Console.WriteLine("✓ Test completed");
Console.WriteLine("═══════════════════════════════════════════════════════════");
