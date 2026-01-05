namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Represents an EOS blockchain transaction
/// </summary>
public sealed record Transaction
{
    /// <summary>
    /// Transaction expiration time
    /// </summary>
    public required DateTime Expiration { get; init; }

    /// <summary>
    /// Reference block number (last 16 bits)
    /// </summary>
    public required ushort RefBlockNum { get; init; }

    /// <summary>
    /// Reference block prefix
    /// </summary>
    public required uint RefBlockPrefix { get; init; }

    /// <summary>
    /// Maximum net usage in words
    /// </summary>
    public uint MaxNetUsageWords { get; init; }

    /// <summary>
    /// Maximum CPU usage in milliseconds
    /// </summary>
    public byte MaxCpuUsageMs { get; init; }

    /// <summary>
    /// Delay in seconds before transaction can execute
    /// </summary>
    public uint DelaySec { get; init; }

    /// <summary>
    /// Context-free actions
    /// </summary>
    public IReadOnlyList<Action> ContextFreeActions { get; init; } = Array.Empty<Action>();

    /// <summary>
    /// Transaction actions
    /// </summary>
    public required IReadOnlyList<Action> Actions { get; init; }

    /// <summary>
    /// Transaction extensions
    /// </summary>
    public IReadOnlyList<Extension> TransactionExtensions { get; init; } = Array.Empty<Extension>();
}

/// <summary>
/// Represents a signed transaction
/// </summary>
public sealed record SignedTransaction
{
    /// <summary>
    /// The underlying transaction
    /// </summary>
    public required Transaction Transaction { get; init; }

    /// <summary>
    /// Transaction signatures
    /// </summary>
    public required IReadOnlyList<string> Signatures { get; init; }

    /// <summary>
    /// Packed transaction bytes
    /// </summary>
    public required byte[] PackedTransaction { get; init; }
}

/// <summary>
/// Represents an action in a transaction
/// </summary>
public sealed record Action
{
    /// <summary>
    /// Account that owns the action contract
    /// </summary>
    public required string Account { get; init; }

    /// <summary>
    /// Action name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Authorization requirements
    /// </summary>
    public required IReadOnlyList<PermissionLevel> Authorization { get; init; }

    /// <summary>
    /// Action data (hex string or object)
    /// </summary>
    public required object Data { get; init; }
}

/// <summary>
/// Represents a permission level
/// </summary>
public sealed record PermissionLevel
{
    /// <summary>
    /// Account name
    /// </summary>
    public required string Actor { get; init; }

    /// <summary>
    /// Permission name (e.g., "active", "owner")
    /// </summary>
    public required string Permission { get; init; }
}

/// <summary>
/// Represents a transaction extension
/// </summary>
public sealed record Extension
{
    /// <summary>
    /// Extension type
    /// </summary>
    public required ushort Type { get; init; }

    /// <summary>
    /// Extension data
    /// </summary>
    public required byte[] Data { get; init; }
}
