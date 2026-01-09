using System.Text.Json;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Transactions;

namespace SUS.EOS.Sharp.Services;

/// <summary>
/// Generic Antelope transaction building and signing service
/// Works with any EOSIO/Antelope-based blockchain
/// </summary>
public interface IAntelopeTransactionService
{
    /// <summary>
    /// Build and sign a transaction with typed action data
    /// </summary>
    Task<SignedTransactionResult> BuildAndSignAsync<T>(
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        T data,
        TimeSpan? expiration = null,
        string authority = "active");

    /// <summary>
    /// Build and sign a transaction with pre-serialized binary action data
    /// </summary>
    Task<SignedTransactionResult> BuildAndSignWithBinaryDataAsync(
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        byte[] binaryData,
        TimeSpan? expiration = null,
        string authority = "active");

    /// <summary>
    /// Build and sign a transaction using ABI-based serialization
    /// Fetches the contract's ABI and serializes the data automatically
    /// </summary>
    Task<SignedTransactionResult> BuildAndSignWithAbiAsync(
        IAntelopeBlockchainClient client,
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        object data,
        TimeSpan? expiration = null,
        string authority = "active");

    /// <summary>
    /// Build and sign a transaction using a cached ABI definition
    /// </summary>
    Task<SignedTransactionResult> BuildAndSignWithAbiAsync(
        AbiDefinition abi,
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        object data,
        TimeSpan? expiration = null,
        string authority = "active");

    /// <summary>
    /// Get public key from private key WIF
    /// </summary>
    string GetPublicKey(string privateKeyWif);

    /// <summary>
    /// Calculate ref_block_prefix from block ID
    /// </summary>
    uint CalculateRefBlockPrefix(string blockId);
}

/// <summary>
/// Antelope transaction service implementation
/// </summary>
public class AntelopeTransactionService : IAntelopeTransactionService
{
    /// <summary>
    /// Build and sign a transaction with typed action data
    /// </summary>
    public async Task<SignedTransactionResult> BuildAndSignAsync<T>(
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        T data,
        TimeSpan? expiration = null,
        string authority = "active")
    {
        // Build transaction using library
        var builder = new EosioTransactionBuilder<T>(chainInfo);
        if (expiration.HasValue)
        {
            builder.SetExpiration(expiration.Value);
        }

        builder.AddAction(contract, action, actor, authority, data);
        var transaction = builder.Build();

        // Sign transaction
        var signer = new EosioSignatureProvider(privateKeyWif);
        var signature = signer.SignTransaction(chainInfo.ChainId, transaction);

        // Serialize for blockchain
        var serialized = EosioSerializer.SerializeTransaction(transaction);
        var packedTrx = EosioSerializer.BytesToHexString(serialized);

        return new SignedTransactionResult
        {
            Transaction = transaction,
            Signatures = new List<string> { signature },
            PackedTransaction = packedTrx,
            PublicKey = signer.PublicKey
        };
    }

    /// <summary>
    /// Build and sign a transaction with pre-serialized binary action data
    /// </summary>
    public async Task<SignedTransactionResult> BuildAndSignWithBinaryDataAsync(
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        byte[] binaryData,
        TimeSpan? expiration = null,
        string authority = "active")
    {
        // Build transaction using library with binary data
        var builder = new EosioTransactionBuilder<byte[]>(chainInfo);
        if (expiration.HasValue)
        {
            builder.SetExpiration(expiration.Value);
        }

        builder.AddActionWithBinaryData(contract, action, actor, authority, binaryData);
        var transaction = builder.Build();

        // Sign transaction
        var signer = new EosioSignatureProvider(privateKeyWif);
        var signature = signer.SignTransaction(chainInfo.ChainId, transaction);

        // Serialize for blockchain (with binary data already provided)
        var serialized = EosioSerializer.SerializeTransactionWithBinaryData(transaction);
        var packedTrx = EosioSerializer.BytesToHexString(serialized);

        return new SignedTransactionResult
        {
            Transaction = transaction,
            Signatures = new List<string> { signature },
            PackedTransaction = packedTrx,
            PublicKey = signer.PublicKey
        };
    }

    /// <summary>
    /// Get public key from private key WIF
    /// </summary>
    public string GetPublicKey(string privateKeyWif)
    {
        var key = EosioKey.FromPrivateKey(privateKeyWif);
        return key.PublicKey;
    }

    /// <summary>
    /// Build and sign a transaction using ABI-based serialization
    /// Fetches the contract's ABI and serializes the data automatically
    /// </summary>
    public async Task<SignedTransactionResult> BuildAndSignWithAbiAsync(
        IAntelopeBlockchainClient client,
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        object data,
        TimeSpan? expiration = null,
        string authority = "active")
    {
        // Fetch ABI from chain
        var abi = await client.GetAbiAsync(contract);
        if (abi == null)
            throw new InvalidOperationException($"Could not fetch ABI for contract '{contract}'");

        return await BuildAndSignWithAbiAsync(abi, chainInfo, actor, privateKeyWif, contract, action, data, expiration, authority);
    }

    /// <summary>
    /// Build and sign a transaction using a cached ABI definition
    /// </summary>
    public async Task<SignedTransactionResult> BuildAndSignWithAbiAsync(
        AbiDefinition abi,
        ChainInfo chainInfo,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        object data,
        TimeSpan? expiration = null,
        string authority = "active")
    {
        // Serialize action data using ABI
        var abiSerializer = new AbiSerializer(abi);
        var binaryData = abiSerializer.SerializeActionData(action, data);

        // Build transaction with binary data
        var builder = new EosioTransactionBuilder<byte[]>(chainInfo);
        if (expiration.HasValue)
        {
            builder.SetExpiration(expiration.Value);
        }

        builder.AddActionWithBinaryData(contract, action, actor, authority, binaryData);
        var transaction = builder.Build();

        // Sign transaction
        var signer = new EosioSignatureProvider(privateKeyWif);
        var signature = signer.SignTransaction(chainInfo.ChainId, transaction);

        // Serialize for blockchain
        var serialized = EosioSerializer.SerializeTransactionWithBinaryData(transaction);
        var packedTrx = EosioSerializer.BytesToHexString(serialized);

        return new SignedTransactionResult
        {
            Transaction = transaction,
            Signatures = new List<string> { signature },
            PackedTransaction = packedTrx,
            PublicKey = signer.PublicKey
        };
    }

    /// <summary>
    /// Calculate ref_block_prefix from block ID (first 4 bytes as big-endian uint)
    /// </summary>
    public uint CalculateRefBlockPrefix(string blockId)
    {
        if (string.IsNullOrEmpty(blockId) || blockId.Length < 16)
            throw new ArgumentException("Invalid block ID format", nameof(blockId));

        // Take first 8 characters (4 bytes) and reverse for big-endian
        var prefixHex = blockId[8..16]; // Skip first 4 bytes, take next 4
        var prefixBytes = Convert.FromHexString(prefixHex);
        
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(prefixBytes);
        }
        
        return BitConverter.ToUInt32(prefixBytes);
    }
}

/// <summary>
/// High-level blockchain operations service
/// Combines blockchain client and transaction service for common operations
/// </summary>
public interface IBlockchainOperationsService
{
    /// <summary>
    /// Transfer tokens between accounts
    /// </summary>
    Task<TransactionResult> TransferAsync(
        string from,
        string to,
        string amount,
        string symbol,
        string memo,
        string privateKeyWif,
        string tokenContract = "eosio.token");

    /// <summary>
    /// Stake resources (CPU/NET)
    /// </summary>
    Task<TransactionResult> StakeResourcesAsync(
        string from,
        string receiver,
        string stakeCpu,
        string stakeNet,
        string privateKeyWif);

    /// <summary>
    /// Unstake resources (CPU/NET)
    /// </summary>
    Task<TransactionResult> UnstakeResourcesAsync(
        string from,
        string receiver,
        string unstakeCpu,
        string unstakeNet,
        string privateKeyWif);

    /// <summary>
    /// Get account balances for multiple tokens
    /// </summary>
    Task<Dictionary<string, List<string>>> GetAccountBalancesAsync(
        string account,
        params string[] tokenContracts);

    /// <summary>
    /// Check if account exists on blockchain
    /// </summary>
    Task<bool> AccountExistsAsync(string account);
}

/// <summary>
/// Blockchain operations service implementation
/// </summary>
public class BlockchainOperationsService : IBlockchainOperationsService
{
    private readonly IAntelopeBlockchainClient _client;
    private readonly IAntelopeTransactionService _transactionService;

    /// <summary>
    /// Creates a new <see cref="BlockchainOperationsService"/> with required dependencies
    /// </summary>
    public BlockchainOperationsService(
        IAntelopeBlockchainClient client, 
        IAntelopeTransactionService transactionService)
    {
        _client = client;
        _transactionService = transactionService;
    }

    /// <summary>
    /// Transfer tokens between accounts
    /// </summary>
    public async Task<TransactionResult> TransferAsync(
        string from,
        string to,
        string amount,
        string symbol,
        string memo,
        string privateKeyWif,
        string tokenContract = "eosio.token")
    {
        var chainInfo = await _client.GetInfoAsync();
        
        var transferData = new
        {
            from = from,
            to = to,
            quantity = $"{amount} {symbol}",
            memo = memo
        };

        var signedTx = await _transactionService.BuildAndSignAsync(
            chainInfo, from, privateKeyWif, tokenContract, "transfer", transferData);

        return await _client.PushTransactionAsync(new
        {
            signatures = signedTx.Signatures,
            compression = 0,
            packed_context_free_data = "",
            packed_trx = signedTx.PackedTransaction
        });
    }

    /// <summary>
    /// Stake resources (CPU/NET)
    /// </summary>
    public async Task<TransactionResult> StakeResourcesAsync(
        string from,
        string receiver,
        string stakeCpu,
        string stakeNet,
        string privateKeyWif)
    {
        var chainInfo = await _client.GetInfoAsync();
        
        var stakeData = new
        {
            from = from,
            receiver = receiver,
            stake_cpu_quantity = stakeCpu,
            stake_net_quantity = stakeNet,
            transfer = false
        };

        var signedTx = await _transactionService.BuildAndSignAsync(
            chainInfo, from, privateKeyWif, "eosio", "delegatebw", stakeData);

        return await _client.PushTransactionAsync(new
        {
            signatures = signedTx.Signatures,
            compression = 0,
            packed_context_free_data = "",
            packed_trx = signedTx.PackedTransaction
        });
    }

    /// <summary>
    /// Unstake resources (CPU/NET)
    /// </summary>
    public async Task<TransactionResult> UnstakeResourcesAsync(
        string from,
        string receiver,
        string unstakeCpu,
        string unstakeNet,
        string privateKeyWif)
    {
        var chainInfo = await _client.GetInfoAsync();
        
        var unstakeData = new
        {
            from = from,
            receiver = receiver,
            unstake_cpu_quantity = unstakeCpu,
            unstake_net_quantity = unstakeNet
        };

        var signedTx = await _transactionService.BuildAndSignAsync(
            chainInfo, from, privateKeyWif, "eosio", "undelegatebw", unstakeData);

        return await _client.PushTransactionAsync(new
        {
            signatures = signedTx.Signatures,
            compression = 0,
            packed_context_free_data = "",
            packed_trx = signedTx.PackedTransaction
        });
    }

    /// <summary>
    /// Get account balances for multiple tokens
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetAccountBalancesAsync(
        string account,
        params string[] tokenContracts)
    {
        var balances = new Dictionary<string, List<string>>();
        
        var defaultContracts = tokenContracts.Length > 0 
            ? tokenContracts 
            : new[] { "eosio.token" };

        foreach (var contract in defaultContracts)
        {
            try
            {
                var contractBalances = await _client.GetCurrencyBalanceAsync(contract, account);
                balances[contract] = contractBalances;
            }
            catch
            {
                // Contract may not exist or account may have no balance
                balances[contract] = new List<string>();
            }
        }

        return balances;
    }

    /// <summary>
    /// Check if account exists on blockchain
    /// </summary>
    public async Task<bool> AccountExistsAsync(string account)
    {
        try
        {
            await _client.GetAccountAsync(account);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Result of transaction building and signing
/// Contains the typed or binary transaction, signatures, packed transaction hex, and public key used to sign
/// </summary>
public record SignedTransactionResult
{
    /// <summary>
    /// The transaction object (typed or binary payload)
    /// </summary>
    public required object Transaction { get; init; }

    /// <summary>
    /// Signatures produced when signing the transaction
    /// </summary>
    public required List<string> Signatures { get; init; }

    /// <summary>
    /// Packed transaction hex string suitable for PushTransaction
    /// </summary>
    public required string PackedTransaction { get; init; }

    /// <summary>
    /// Public key (WIF or EOSIO format) corresponding to the signing private key
    /// </summary>
    public required string PublicKey { get; init; }
}