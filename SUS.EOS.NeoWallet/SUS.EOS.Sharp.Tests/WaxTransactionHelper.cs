using System.Text.Json;
using SUS.EOS.Sharp.Cryptography;
using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;
using SUS.EOS.Sharp.Services;
using SUS.EOS.Sharp.Signatures;
using SUS.EOS.Sharp.Transactions;

namespace SUS.EOS.Sharp.Tests;

/// <summary>
/// Adapter for WAX blockchain operations using library classes
/// </summary>
public static class WaxTransactionHelper
{
    private static readonly AntelopeTransactionService _transactionService = new();

    /// <summary>
    /// Converts WaxChainInfo to the library's ChainInfo
    /// </summary>
    public static SUS.EOS.Sharp.Models.ChainInfo ToLibraryChainInfo(WaxChainInfo chainInfo, uint refBlockPrefix)
    {
        return new SUS.EOS.Sharp.Models.ChainInfo
        {
            ServerVersion = chainInfo.ServerVersion,
            ChainId = chainInfo.ChainId,
            HeadBlockNum = (uint)chainInfo.HeadBlockNum,
            HeadBlockId = chainInfo.HeadBlockId,
            HeadBlockTime = chainInfo.HeadBlockTime,
            HeadBlockProducer = chainInfo.HeadBlockProducer,
            LastIrreversibleBlockNum = (uint)chainInfo.LastIrreversibleBlockNum,
            LastIrreversibleBlockId = chainInfo.LastIrreversibleBlockId ?? string.Empty,
            VirtualBlockCpuLimit = 0,
            VirtualBlockNetLimit = 0,
            BlockCpuLimit = 0,
            BlockNetLimit = 0,
            RefBlockPrefix = refBlockPrefix
        };
    }

    /// <summary>
    /// Builds and signs a transaction using ABI-based serialization (recommended)
    /// </summary>
    public static async Task<SignedTransactionResult> BuildAndSignWithAbiAsync(
        WaxBlockchainClient client,
        WaxChainInfo chainInfo,
        uint refBlockPrefix,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        object data,
        TimeSpan? expiration = null)
    {
        var libraryChainInfo = ToLibraryChainInfo(chainInfo, refBlockPrefix);

        // Fetch ABI from chain
        var abi = await client.GetAbiAsync(contract);
        if (abi == null)
            throw new InvalidOperationException($"Could not fetch ABI for contract '{contract}'");

        // Use library's ABI-based signing
        return await _transactionService.BuildAndSignWithAbiAsync(
            abi,
            libraryChainInfo,
            actor,
            privateKeyWif,
            contract,
            action,
            data,
            expiration);
    }

    /// <summary>
    /// Builds and signs a transaction with pre-serialized binary action data
    /// </summary>
    public static SignedTransactionResult BuildAndSignWithBinaryData(
        WaxChainInfo chainInfo,
        uint refBlockPrefix,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        string binaryDataHex,
        TimeSpan? expiration = null)
    {
        var libraryChainInfo = ToLibraryChainInfo(chainInfo, refBlockPrefix);

        // Build transaction using library with binary data
        var builder = new EosioTransactionBuilder<byte[]>(libraryChainInfo);
        if (expiration.HasValue)
        {
            builder.SetExpiration(expiration.Value);
        }

        // Convert hex to bytes for the action data
        var binaryData = Convert.FromHexString(binaryDataHex);
        builder.AddActionWithBinaryData(contract, action, actor, "active", binaryData);
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
    /// Builds and signs a transaction (with JSON data - converted locally, may not work for all contracts)
    /// </summary>
    public static SignedTransactionResult BuildAndSign<T>(
        WaxChainInfo chainInfo,
        uint refBlockPrefix,
        string actor,
        string privateKeyWif,
        string contract,
        string action,
        T data,
        TimeSpan? expiration = null)
    {
        var libraryChainInfo = ToLibraryChainInfo(chainInfo, refBlockPrefix);

        // Build transaction using library
        var builder = new EosioTransactionBuilder<T>(libraryChainInfo);
        if (expiration.HasValue)
        {
            builder.SetExpiration(expiration.Value);
        }

        builder.AddAction(contract, action, actor, "active", data);
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
    /// Converts WIF key to public key
    /// </summary>
    public static string GetPublicKey(string privateKeyWif)
    {
        var key = EosioKey.FromPrivateKey(privateKeyWif);
        return key.PublicKey;
    }
}
