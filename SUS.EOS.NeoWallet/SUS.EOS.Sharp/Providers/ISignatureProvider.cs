namespace SUS.EOS.Sharp.Providers;

/// <summary>
/// Interface for signature providers that sign transactions
/// </summary>
public interface ISignatureProvider
{
    /// <summary>
    /// Gets available public keys from this provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available public keys</returns>
    Task<IReadOnlyList<string>> GetAvailableKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs transaction data with the required keys
    /// </summary>
    /// <param name="chainId">Blockchain chain ID</param>
    /// <param name="requiredKeys">Public keys required to sign</param>
    /// <param name="signBytes">Serialized transaction bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of signatures</returns>
    Task<IReadOnlyList<string>> SignAsync(
        string chainId,
        IEnumerable<string> requiredKeys,
        byte[] signBytes,
        CancellationToken cancellationToken = default);
}
