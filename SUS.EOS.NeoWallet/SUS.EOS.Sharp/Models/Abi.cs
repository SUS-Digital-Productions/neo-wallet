using System.Text.Json.Serialization;

namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Complete ABI definition for an EOSIO/Antelope smart contract
/// </summary>
public sealed class AbiDefinition
{
    /// <summary>
    /// ABI version string (e.g. 'eosio::abi/1.1')
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "eosio::abi/1.1";

    /// <summary>
    /// Type aliases defined in the ABI
    /// </summary>
    [JsonPropertyName("types")]
    public List<AbiTypeDef> Types { get; set; } = new();

    /// <summary>
    /// Struct definitions in the ABI
    /// </summary>
    [JsonPropertyName("structs")]
    public List<AbiStruct> Structs { get; set; } = new();

    /// <summary>
    /// Actions exposed by the contract
    /// </summary>
    [JsonPropertyName("actions")]
    public List<AbiAction> Actions { get; set; } = new();

    /// <summary>
    /// Tables defined by the contract
    /// </summary>
    [JsonPropertyName("tables")]
    public List<AbiTable> Tables { get; set; } = new();

    /// <summary>
    /// Ricardian clauses included in the ABI
    /// </summary>
    [JsonPropertyName("ricardian_clauses")]
    public List<AbiRicardianClause> RicardianClauses { get; set; } = new();

    /// <summary>
    /// Error messages defined for the contract
    /// </summary>
    [JsonPropertyName("error_messages")]
    public List<AbiErrorMessage> ErrorMessages { get; set; } = new();

    /// <summary>
    /// ABI extension entries (unused placeholder)
    /// </summary>
    [JsonPropertyName("abi_extensions")]
    public List<object> AbiExtensions { get; set; } = new();

    /// <summary>
    /// Variant type definitions
    /// </summary>
    [JsonPropertyName("variants")]
    public List<AbiVariant> Variants { get; set; } = new();

    /// <summary>
    /// Action result definitions
    /// </summary>
    [JsonPropertyName("action_results")]
    public List<AbiActionResult> ActionResults { get; set; } = new();

    /// <summary>
    /// Gets a struct definition by name, following type aliases
    /// </summary>
    public AbiStruct? GetStruct(string name)
    {
        // First check for type alias
        var typeDef = Types.FirstOrDefault(t => t.NewTypeName == name);
        var resolvedName = typeDef?.Type ?? name;
        
        return Structs.FirstOrDefault(s => s.Name == resolvedName);
    }

    /// <summary>
    /// Gets the struct name for an action
    /// </summary>
    public string? GetActionType(string actionName)
    {
        var action = Actions.FirstOrDefault(a => a.Name == actionName);
        return action?.Type;
    }

    /// <summary>
    /// Resolves a type name to its base type, following aliases
    /// </summary>
    public string ResolveType(string typeName)
    {
        // Strip array notation for resolution
        var isArray = typeName.EndsWith("[]");
        var isOptional = typeName.EndsWith("?");
        var baseName = typeName.TrimEnd('[', ']', '?');

        // Check for type alias
        var typeDef = Types.FirstOrDefault(t => t.NewTypeName == baseName);
        if (typeDef != null)
        {
            baseName = ResolveType(typeDef.Type);
        }

        // Re-add modifiers
        if (isArray) baseName += "[]";
        if (isOptional) baseName += "?";

        return baseName;
    }
}

/// <summary>
/// Type definition/alias in ABI
/// </summary>
public sealed class AbiTypeDef
{
    /// <summary>
    /// Alias name for a type (e.g. 'account_name')
    /// </summary>
    [JsonPropertyName("new_type_name")]
    public string NewTypeName { get; set; } = string.Empty;

    /// <summary>
    /// The underlying type name (e.g. 'name')
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Struct definition in ABI
/// </summary>
public sealed class AbiStruct
{
    /// <summary>
    /// Struct name defined in the ABI
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Base struct name (for inheritance), if any
    /// </summary>
    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    /// <summary>
    /// Fields declared on this struct
    /// </summary>
    [JsonPropertyName("fields")]
    public List<AbiField> Fields { get; set; } = new();

    /// <summary>
    /// Gets all fields including inherited fields from base struct
    /// </summary>
    public IEnumerable<AbiField> GetAllFields(AbiDefinition abi)
    {
        if (!string.IsNullOrEmpty(Base))
        {
            var baseStruct = abi.GetStruct(Base);
            if (baseStruct != null)
            {
                foreach (var field in baseStruct.GetAllFields(abi))
                {
                    yield return field;
                }
            }
        }

        foreach (var field in Fields)
        {
            yield return field;
        }
    }
}

/// <summary>
/// Field definition in a struct
/// </summary>
public sealed class AbiField
{
    /// <summary>
    /// Field name in the struct
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field type name (may be alias or complex type)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Action definition in ABI
/// </summary>
public sealed class AbiAction
{
    /// <summary>
    /// Action name (e.g. 'transfer')
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Struct type name for action data
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Ricardian contract text for this action
    /// </summary>
    [JsonPropertyName("ricardian_contract")]
    public string RicardianContract { get; set; } = string.Empty;
}

/// <summary>
/// Table definition in ABI
/// </summary>
public sealed class AbiTable
{
    /// <summary>
    /// Table name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type name for table rows
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Index type (e.g., 'i64')
    /// </summary>
    [JsonPropertyName("index_type")]
    public string IndexType { get; set; } = string.Empty;

    /// <summary>
    /// Names of the keys used by the table
    /// </summary>
    [JsonPropertyName("key_names")]
    public List<string> KeyNames { get; set; } = new();

    /// <summary>
    /// Types of the keys used by the table
    /// </summary>
    [JsonPropertyName("key_types")]
    public List<string> KeyTypes { get; set; } = new();
}

/// <summary>
/// Ricardian clause in ABI
/// </summary>
public sealed class AbiRicardianClause
{
    /// <summary>
    /// Ricardian clause identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Ricardian clause body text
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Error message definition in ABI
/// </summary>
public sealed class AbiErrorMessage
{
    /// <summary>
    /// Numerical error code
    /// </summary>
    [JsonPropertyName("error_code")]
    public ulong ErrorCode { get; set; }

    /// <summary>
    /// Error message text
    /// </summary>
    [JsonPropertyName("error_msg")]
    public string ErrorMsg { get; set; } = string.Empty;
}

/// <summary>
/// Variant type definition in ABI
/// </summary>
public sealed class AbiVariant
{
    /// <summary>
    /// Variant name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Possible types for this variant
    /// </summary>
    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = new();
}

/// <summary>
/// Action result definition in ABI
/// </summary>
public sealed class AbiActionResult
{
    /// <summary>
    /// Action result name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Result type name
    /// </summary>
    [JsonPropertyName("result_type")]
    public string ResultType { get; set; } = string.Empty;
}

/// <summary>
/// Response from get_abi endpoint
/// </summary>
public sealed class GetAbiResponse
{
    /// <summary>
    /// Account name owning the contract
    /// </summary>
    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// ABI definition for the contract
    /// </summary>
    [JsonPropertyName("abi")]
    public AbiDefinition? Abi { get; set; }
}

/// <summary>
/// Response from abi_json_to_bin endpoint
/// </summary>
public sealed class AbiJsonToBinResponse
{
    /// <summary>
    /// Binary arguments encoded as hex/base64 as returned by abi_json_to_bin
    /// </summary>
    [JsonPropertyName("binargs")]
    public string BinArgs { get; set; } = string.Empty;
}

/// <summary>
/// Response from abi_bin_to_json endpoint
/// </summary>
public sealed class AbiBinToJsonResponse
{
    /// <summary>
    /// Decoded action arguments
    /// </summary>
    [JsonPropertyName("args")]
    public object? Args { get; set; }
}
