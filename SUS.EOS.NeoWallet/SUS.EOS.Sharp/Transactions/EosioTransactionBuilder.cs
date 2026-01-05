using SUS.EOS.Sharp.Models;
using SUS.EOS.Sharp.Serialization;

namespace SUS.EOS.Sharp.Transactions;

/// <summary>
/// Builder for EOSIO transactions
/// </summary>
public sealed class EosioTransactionBuilder<T>
{
    private readonly ChainInfo _chainInfo;
    private DateTime _expiration;
    private readonly List<EosioAction<T>> _actions = new();

    public EosioTransactionBuilder(ChainInfo chainInfo)
    {
        _chainInfo = chainInfo;
        // Use blockchain time, not local system time
        // Ensure we're working in UTC
        var blockchainTime = DateTime.SpecifyKind(chainInfo.HeadBlockTime, DateTimeKind.Utc);
        _expiration = blockchainTime.AddSeconds(30);
    }

    /// <summary>
    /// Sets the transaction expiration time
    /// </summary>
    public EosioTransactionBuilder<T> SetExpiration(TimeSpan expiresIn)
    {
        // Use blockchain time as base, not local system time
        var blockchainTime = DateTime.SpecifyKind(_chainInfo.HeadBlockTime, DateTimeKind.Utc);
        _expiration = blockchainTime.Add(expiresIn);
        return this;
    }

    /// <summary>
    /// Adds an action to the transaction
    /// </summary>
    public EosioTransactionBuilder<T> AddAction(
        string contract,
        string action,
        string actor,
        string permission,
        T? data)
    {
        _actions.Add(new EosioAction<T>
        {
            Account = contract,
            Name = action,
            Authorization = new List<EosioAuthorization>
            {
                new() { Actor = actor, Permission = permission }
            },
            Data = data
        });
        return this;
    }

    /// <summary>
    /// Adds an action with pre-serialized binary data to the transaction.
    /// Use this when you have already serialized the action data using ABI.
    /// For byte[] data type, the data will be used directly as binary.
    /// </summary>
    public EosioTransactionBuilder<T> AddActionWithBinaryData(
        string contract,
        string action,
        string actor,
        string permission,
        T? binaryData)
    {
        _actions.Add(new EosioAction<T>
        {
            Account = contract,
            Name = action,
            Authorization = new List<EosioAuthorization>
            {
                new() { Actor = actor, Permission = permission }
            },
            Data = binaryData,
            IsBinaryData = true  // Mark as pre-serialized binary
        });
        return this;
    }

    /// <summary>
    /// Builds the transaction object
    /// </summary>
    public EosioTransaction<T> Build()
    {
        return new EosioTransaction<T>
        {
            // Expiration is already in UTC, just format it
            Expiration = _expiration.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
            RefBlockNum = (ushort)(_chainInfo.HeadBlockNum & 0xFFFF),
            RefBlockPrefix = _chainInfo.RefBlockPrefix,
            MaxNetUsageWords = 0,
            MaxCpuUsageMs = 0,
            DelaySec = 0,
            ContextFreeActions = new List<EosioAction<T>>(),
            Actions = _actions,
            TransactionExtensions = new List<object>()
        };
    }
}
