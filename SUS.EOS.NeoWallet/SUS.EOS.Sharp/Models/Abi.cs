using System.Text.Json.Serialization;

namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Complete ABI definition for an EOSIO/Antelope smart contract
/// </summary>
public sealed class AbiDefinition
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "eosio::abi/1.1";

    [JsonPropertyName("types")]
    public List<AbiTypeDef> Types { get; set; } = new();

    [JsonPropertyName("structs")]
    public List<AbiStruct> Structs { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<AbiAction> Actions { get; set; } = new();

    [JsonPropertyName("tables")]
    public List<AbiTable> Tables { get; set; } = new();

    [JsonPropertyName("ricardian_clauses")]
    public List<AbiRicardianClause> RicardianClauses { get; set; } = new();

    [JsonPropertyName("error_messages")]
    public List<AbiErrorMessage> ErrorMessages { get; set; } = new();

    [JsonPropertyName("abi_extensions")]
    public List<object> AbiExtensions { get; set; } = new();

    [JsonPropertyName("variants")]
    public List<AbiVariant> Variants { get; set; } = new();

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
    [JsonPropertyName("new_type_name")]
    public string NewTypeName { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Struct definition in ABI
/// </summary>
public sealed class AbiStruct
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

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
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Action definition in ABI
/// </summary>
public sealed class AbiAction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("ricardian_contract")]
    public string RicardianContract { get; set; } = string.Empty;
}

/// <summary>
/// Table definition in ABI
/// </summary>
public sealed class AbiTable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("index_type")]
    public string IndexType { get; set; } = string.Empty;

    [JsonPropertyName("key_names")]
    public List<string> KeyNames { get; set; } = new();

    [JsonPropertyName("key_types")]
    public List<string> KeyTypes { get; set; } = new();
}

/// <summary>
/// Ricardian clause in ABI
/// </summary>
public sealed class AbiRicardianClause
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Error message definition in ABI
/// </summary>
public sealed class AbiErrorMessage
{
    [JsonPropertyName("error_code")]
    public ulong ErrorCode { get; set; }

    [JsonPropertyName("error_msg")]
    public string ErrorMsg { get; set; } = string.Empty;
}

/// <summary>
/// Variant type definition in ABI
/// </summary>
public sealed class AbiVariant
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = new();
}

/// <summary>
/// Action result definition in ABI
/// </summary>
public sealed class AbiActionResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("result_type")]
    public string ResultType { get; set; } = string.Empty;
}

/// <summary>
/// Response from get_abi endpoint
/// </summary>
public sealed class GetAbiResponse
{
    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [JsonPropertyName("abi")]
    public AbiDefinition? Abi { get; set; }
}

/// <summary>
/// Response from abi_json_to_bin endpoint
/// </summary>
public sealed class AbiJsonToBinResponse
{
    [JsonPropertyName("binargs")]
    public string BinArgs { get; set; } = string.Empty;
}

/// <summary>
/// Response from abi_bin_to_json endpoint
/// </summary>
public sealed class AbiBinToJsonResponse
{
    [JsonPropertyName("args")]
    public object? Args { get; set; }
}
