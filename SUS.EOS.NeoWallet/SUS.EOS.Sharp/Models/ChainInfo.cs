namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Represents blockchain information
/// </summary>
public sealed record ChainInfo
{
    /// <summary>
    /// Server version hash
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Chain ID (64-character hex string)
    /// </summary>
    public required string ChainId { get; init; }

    /// <summary>
    /// Current head block number
    /// </summary>
    public required uint HeadBlockNum { get; init; }

    /// <summary>
    /// Last irreversible block number
    /// </summary>
    public required uint LastIrreversibleBlockNum { get; init; }

    /// <summary>
    /// Last irreversible block ID
    /// </summary>
    public required string LastIrreversibleBlockId { get; init; }

    /// <summary>
    /// Current head block ID
    /// </summary>
    public required string HeadBlockId { get; init; }

    /// <summary>
    /// Head block timestamp
    /// </summary>
    public required DateTime HeadBlockTime { get; init; }

    /// <summary>
    /// Current head block producer
    /// </summary>
    public required string HeadBlockProducer { get; init; }

    /// <summary>
    /// Virtual block CPU limit
    /// </summary>
    public required ulong VirtualBlockCpuLimit { get; init; }

    /// <summary>
    /// Virtual block NET limit
    /// </summary>
    public required ulong VirtualBlockNetLimit { get; init; }

    /// <summary>
    /// Block CPU limit
    /// </summary>
    public required ulong BlockCpuLimit { get; init; }

    /// <summary>
    /// Block NET limit
    /// </summary>
    public required ulong BlockNetLimit { get; init; }

    /// <summary>
    /// Server version string
    /// </summary>
    public string? ServerVersionString { get; init; }

    /// <summary>
    /// Fork database head block number
    /// </summary>
    public uint? ForkDbHeadBlockNum { get; init; }

    /// <summary>
    /// Fork database head block ID
    /// </summary>
    public string? ForkDbHeadBlockId { get; init; }

    /// <summary>
    /// Reference block prefix for TAPOS (from get_block API)
    /// </summary>
    public uint RefBlockPrefix { get; init; }
}
