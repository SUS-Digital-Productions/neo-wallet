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

    /// <summary>
    /// Create a new transaction builder initialized with the given chain info
    /// </summary>
    /// <param name="chainInfo">Chain information used for TAPOS and timing</param>
    public EosioTransactionBuilder(ChainInfo chainInfo)
    {
        _chainInfo = chainInfo;
        _expiration = GetExpirationBaseTime().AddSeconds(30);
    }

    /// <summary>
    /// Sets the transaction expiration time
    /// </summary>
    public EosioTransactionBuilder<T> SetExpiration(TimeSpan expiresIn)
    {
        _expiration = GetExpirationBaseTime().Add(expiresIn);
        return this;
    }

    private DateTime GetExpirationBaseTime()
    {
        var blockchainTime = DateTime.SpecifyKind(_chainInfo.HeadBlockTime, DateTimeKind.Utc);
        var utcNow = DateTime.UtcNow;
        return blockchainTime > utcNow ? blockchainTime : utcNow;
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
            // TAPOS: Use LastIrreversibleBlockNum, NOT HeadBlockNum
            RefBlockNum = (ushort)(_chainInfo.LastIrreversibleBlockNum & 0xFFFF),
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
