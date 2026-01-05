namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Represents an EOS account
/// </summary>
public sealed record Account
{
    /// <summary>
    /// Account name
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Account creation timestamp
    /// </summary>
    public required DateTime Created { get; init; }

    /// <summary>
    /// CPU limit (in microseconds)
    /// </summary>
    public required ResourceLimit CpuLimit { get; init; }

    /// <summary>
    /// NET limit (in bytes)
    /// </summary>
    public required ResourceLimit NetLimit { get; init; }

    /// <summary>
    /// RAM usage (in bytes)
    /// </summary>
    public required long RamUsage { get; init; }

    /// <summary>
    /// RAM quota (in bytes)
    /// </summary>
    public required long RamQuota { get; init; }

    /// <summary>
    /// Core liquid balance (e.g., "100.0000 EOS")
    /// </summary>
    public string? CoreLiquidBalance { get; init; }

    /// <summary>
    /// Account permissions
    /// </summary>
    public required IReadOnlyList<Permission> Permissions { get; init; }
}

/// <summary>
/// Represents a resource limit
/// </summary>
public sealed record ResourceLimit
{
    /// <summary>
    /// Current usage
    /// </summary>
    public required long Used { get; init; }

    /// <summary>
    /// Available limit
    /// </summary>
    public required long Available { get; init; }

    /// <summary>
    /// Maximum limit
    /// </summary>
    public required long Max { get; init; }
}

/// <summary>
/// Represents an account permission
/// </summary>
public sealed record Permission
{
    /// <summary>
    /// Permission name
    /// </summary>
    public required string PermName { get; init; }

    /// <summary>
    /// Parent permission name
    /// </summary>
    public required string Parent { get; init; }

    /// <summary>
    /// Required authority
    /// </summary>
    public required Authority RequiredAuth { get; init; }
}

/// <summary>
/// Represents an authority structure
/// </summary>
public sealed record Authority
{
    /// <summary>
    /// Threshold for this authority
    /// </summary>
    public required uint Threshold { get; init; }

    /// <summary>
    /// Key-weight pairs
    /// </summary>
    public required IReadOnlyList<KeyWeight> Keys { get; init; }

    /// <summary>
    /// Account-permission-weight tuples
    /// </summary>
    public required IReadOnlyList<PermissionLevelWeight> Accounts { get; init; }
}

/// <summary>
/// Represents a key with its weight
/// </summary>
public sealed record KeyWeight
{
    /// <summary>
    /// Public key
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Weight for this key
    /// </summary>
    public required uint Weight { get; init; }
}

/// <summary>
/// Represents an account permission with its weight
/// </summary>
public sealed record PermissionLevelWeight
{
    /// <summary>
    /// Permission level
    /// </summary>
    public required PermissionLevel Permission { get; init; }

    /// <summary>
    /// Weight for this permission
    /// </summary>
    public required uint Weight { get; init; }
}
