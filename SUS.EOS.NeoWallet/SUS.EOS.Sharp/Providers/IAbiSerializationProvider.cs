using SUS.EOS.Sharp.Models;

namespace SUS.EOS.Sharp.Providers;

/// <summary>
/// Interface for ABI serialization/deserialization
/// </summary>
public interface IAbiSerializationProvider
{
    /// <summary>
    /// Serializes a transaction to packed bytes
    /// </summary>
    /// <param name="transaction">Transaction to serialize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Packed transaction bytes</returns>
    Task<byte[]> SerializeTransactionAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes packed transaction bytes
    /// </summary>
    /// <param name="packedTransaction">Packed transaction hex string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized transaction</returns>
    Task<Transaction> DeserializeTransactionAsync(
        string packedTransaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes action data using ABI schema
    /// </summary>
    /// <param name="action">Action to serialize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Serialized action data</returns>
    Task<byte[]> SerializeActionDataAsync(
        Models.Action action,
        CancellationToken cancellationToken = default);
}
