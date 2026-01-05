using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using SUS.EOS.Sharp.Models;

namespace SUS.EOS.Sharp.Serialization;

/// <summary>
/// ABI-based binary serializer for EOSIO/Antelope action data.
/// Serializes action data based on the contract's ABI definition.
/// </summary>
public sealed class AbiSerializer
{
    private readonly AbiDefinition _abi;
    private readonly Dictionary<string, AbiStruct> _structCache = new();
    
    /// <summary>
    /// Creates a new ABI serializer with the given ABI definition
    /// </summary>
    public AbiSerializer(AbiDefinition abi)
    {
        _abi = abi ?? throw new ArgumentNullException(nameof(abi));
        
        // Build struct cache for faster lookups
        foreach (var s in _abi.Structs)
        {
            _structCache[s.Name] = s;
        }
    }

    /// <summary>
    /// Serializes action data based on the ABI
    /// </summary>
    /// <param name="actionName">The action name to serialize for</param>
    /// <param name="data">The data object (dictionary, anonymous object, or JsonElement)</param>
    /// <returns>Binary serialized action data</returns>
    public byte[] SerializeActionData(string actionName, object data)
    {
        var structType = _abi.GetActionType(actionName);
        if (string.IsNullOrEmpty(structType))
            throw new InvalidOperationException($"Action '{actionName}' not found in ABI");

        return SerializeStruct(structType, data);
    }

    /// <summary>
    /// Serializes a struct type to binary
    /// </summary>
    public byte[] SerializeStruct(string structName, object data)
    {
        var resolvedName = _abi.ResolveType(structName);
        var structDef = _abi.GetStruct(resolvedName);
        
        if (structDef == null)
            throw new InvalidOperationException($"Struct '{structName}' not found in ABI");

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        var dataDict = ToDataDictionary(data);
        var fields = structDef.GetAllFields(_abi).ToList();

        foreach (var field in fields)
        {
            if (!dataDict.TryGetValue(field.Name, out var value))
            {
                throw new InvalidOperationException($"Missing field '{field.Name}' in struct '{structName}'");
            }

            SerializeType(writer, field.Type, value);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes binary data back to a struct
    /// </summary>
    public Dictionary<string, object?> DeserializeStruct(string structName, byte[] data)
    {
        var resolvedName = _abi.ResolveType(structName);
        var structDef = _abi.GetStruct(resolvedName);
        
        if (structDef == null)
            throw new InvalidOperationException($"Struct '{structName}' not found in ABI");

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var result = new Dictionary<string, object?>();
        var fields = structDef.GetAllFields(_abi).ToList();

        foreach (var field in fields)
        {
            result[field.Name] = DeserializeType(reader, field.Type);
        }

        return result;
    }

    /// <summary>
    /// Serializes a single typed value
    /// </summary>
    public void SerializeType(BinaryWriter writer, string typeName, object? value)
    {
        var resolvedType = _abi.ResolveType(typeName);
        
        // Handle optional types
        if (resolvedType.EndsWith("?"))
        {
            var innerType = resolvedType.TrimEnd('?');
            if (value == null)
            {
                writer.Write((byte)0); // not present
                return;
            }
            writer.Write((byte)1); // present
            SerializeType(writer, innerType, value);
            return;
        }

        // Handle array types
        if (resolvedType.EndsWith("[]"))
        {
            var innerType = resolvedType[..^2];
            var array = ToArray(value);
            WriteVarUint32(writer, (uint)array.Count);
            foreach (var item in array)
            {
                SerializeType(writer, innerType, item);
            }
            return;
        }

        // Handle built-in types
        switch (resolvedType)
        {
            case "bool":
                writer.Write(ConvertToBool(value) ? (byte)1 : (byte)0);
                break;
            case "int8":
                writer.Write(ConvertToSByte(value));
                break;
            case "uint8":
                writer.Write(ConvertToByte(value));
                break;
            case "int16":
                writer.Write(ConvertToInt16(value));
                break;
            case "uint16":
                writer.Write(ConvertToUInt16(value));
                break;
            case "int32":
                writer.Write(ConvertToInt32(value));
                break;
            case "uint32":
                writer.Write(ConvertToUInt32(value));
                break;
            case "int64":
                writer.Write(ConvertToInt64(value));
                break;
            case "uint64":
                writer.Write(ConvertToUInt64(value));
                break;
            case "int128":
                WriteInt128(writer, ConvertToInt128(value));
                break;
            case "uint128":
                WriteUInt128(writer, ConvertToUInt128(value));
                break;
            case "float32":
                writer.Write(ConvertToFloat(value));
                break;
            case "float64":
                writer.Write(ConvertToDouble(value));
                break;
            case "float128":
                WriteFloat128(writer, value);
                break;
            case "varint32":
                WriteVarInt32(writer, ConvertToInt32(value));
                break;
            case "varuint32":
                WriteVarUint32(writer, ConvertToUInt32(value));
                break;
            case "name":
                writer.Write(EosioSerializer.NameToUInt64(value?.ToString() ?? string.Empty));
                break;
            case "string":
                WriteString(writer, value?.ToString() ?? string.Empty);
                break;
            case "bytes":
                WriteBytes(writer, ConvertToBytes(value));
                break;
            case "checksum160":
                WriteChecksum(writer, value, 20);
                break;
            case "checksum256":
                WriteChecksum(writer, value, 32);
                break;
            case "checksum512":
                WriteChecksum(writer, value, 64);
                break;
            case "public_key":
                WritePublicKey(writer, value?.ToString() ?? string.Empty);
                break;
            case "signature":
                WriteSignature(writer, value?.ToString() ?? string.Empty);
                break;
            case "symbol":
                WriteSymbol(writer, value?.ToString() ?? string.Empty);
                break;
            case "symbol_code":
                WriteSymbolCode(writer, value?.ToString() ?? string.Empty);
                break;
            case "asset":
                WriteAsset(writer, value?.ToString() ?? string.Empty);
                break;
            case "extended_asset":
                WriteExtendedAsset(writer, value);
                break;
            case "time_point":
                WriteTimePoint(writer, value);
                break;
            case "time_point_sec":
                WriteTimePointSec(writer, value);
                break;
            case "block_timestamp_type":
                WriteBlockTimestamp(writer, value);
                break;
            default:
                // Check if it's a variant
                var variant = _abi.Variants.FirstOrDefault(v => v.Name == resolvedType);
                if (variant != null)
                {
                    SerializeVariant(writer, variant, value);
                    return;
                }

                // Must be a struct
                var structDef = _abi.GetStruct(resolvedType);
                if (structDef != null)
                {
                    SerializeStructInline(writer, structDef, value);
                    return;
                }

                throw new InvalidOperationException($"Unknown type: {resolvedType}");
        }
    }

    /// <summary>
    /// Deserializes a single typed value
    /// </summary>
    public object? DeserializeType(BinaryReader reader, string typeName)
    {
        var resolvedType = _abi.ResolveType(typeName);
        
        // Handle optional types
        if (resolvedType.EndsWith("?"))
        {
            var innerType = resolvedType.TrimEnd('?');
            var present = reader.ReadByte();
            if (present == 0) return null;
            return DeserializeType(reader, innerType);
        }

        // Handle array types
        if (resolvedType.EndsWith("[]"))
        {
            var innerType = resolvedType[..^2];
            var count = ReadVarUint32(reader);
            var result = new List<object?>();
            for (uint i = 0; i < count; i++)
            {
                result.Add(DeserializeType(reader, innerType));
            }
            return result;
        }

        // Handle built-in types
        return resolvedType switch
        {
            "bool" => reader.ReadByte() != 0,
            "int8" => reader.ReadSByte(),
            "uint8" => reader.ReadByte(),
            "int16" => reader.ReadInt16(),
            "uint16" => reader.ReadUInt16(),
            "int32" => reader.ReadInt32(),
            "uint32" => reader.ReadUInt32(),
            "int64" => reader.ReadInt64(),
            "uint64" => reader.ReadUInt64(),
            "int128" => ReadInt128(reader),
            "uint128" => ReadUInt128(reader),
            "float32" => reader.ReadSingle(),
            "float64" => reader.ReadDouble(),
            "float128" => ReadFloat128(reader),
            "varint32" => ReadVarInt32(reader),
            "varuint32" => ReadVarUint32(reader),
            "name" => EosioSerializer.UInt64ToName(reader.ReadUInt64()),
            "string" => ReadString(reader),
            "bytes" => ReadBytes(reader),
            "checksum160" => ReadChecksum(reader, 20),
            "checksum256" => ReadChecksum(reader, 32),
            "checksum512" => ReadChecksum(reader, 64),
            "public_key" => ReadPublicKey(reader),
            "signature" => ReadSignature(reader),
            "symbol" => ReadSymbol(reader),
            "symbol_code" => ReadSymbolCode(reader),
            "asset" => ReadAsset(reader),
            "extended_asset" => ReadExtendedAsset(reader),
            "time_point" => ReadTimePoint(reader),
            "time_point_sec" => ReadTimePointSec(reader),
            "block_timestamp_type" => ReadBlockTimestamp(reader),
            _ => DeserializeComplexType(reader, resolvedType)
        };
    }

    private object? DeserializeComplexType(BinaryReader reader, string typeName)
    {
        // Check if it's a variant
        var variant = _abi.Variants.FirstOrDefault(v => v.Name == typeName);
        if (variant != null)
        {
            return DeserializeVariant(reader, variant);
        }

        // Must be a struct
        var structDef = _abi.GetStruct(typeName);
        if (structDef != null)
        {
            return DeserializeStructInline(reader, structDef);
        }

        throw new InvalidOperationException($"Unknown type: {typeName}");
    }

    #region Struct Serialization

    private void SerializeStructInline(BinaryWriter writer, AbiStruct structDef, object? value)
    {
        var dataDict = ToDataDictionary(value);
        var fields = structDef.GetAllFields(_abi).ToList();

        foreach (var field in fields)
        {
            dataDict.TryGetValue(field.Name, out var fieldValue);
            SerializeType(writer, field.Type, fieldValue);
        }
    }

    private Dictionary<string, object?> DeserializeStructInline(BinaryReader reader, AbiStruct structDef)
    {
        var result = new Dictionary<string, object?>();
        var fields = structDef.GetAllFields(_abi).ToList();

        foreach (var field in fields)
        {
            result[field.Name] = DeserializeType(reader, field.Type);
        }

        return result;
    }

    #endregion

    #region Variant Serialization

    private void SerializeVariant(BinaryWriter writer, AbiVariant variant, object? value)
    {
        if (value == null)
            throw new InvalidOperationException("Variant value cannot be null");

        var dataDict = ToDataDictionary(value);
        
        // Variant format: [type_name, value] or { type_name: value }
        string? variantType = null;
        object? variantValue = null;

        if (dataDict.Count == 1)
        {
            var kvp = dataDict.First();
            variantType = kvp.Key;
            variantValue = kvp.Value;
        }
        else if (dataDict.TryGetValue("type", out var typeObj) && dataDict.TryGetValue("value", out variantValue))
        {
            variantType = typeObj?.ToString();
        }

        if (string.IsNullOrEmpty(variantType))
            throw new InvalidOperationException("Could not determine variant type");

        var typeIndex = variant.Types.IndexOf(variantType);
        if (typeIndex < 0)
            throw new InvalidOperationException($"Variant type '{variantType}' not found in variant '{variant.Name}'");

        WriteVarUint32(writer, (uint)typeIndex);
        SerializeType(writer, variantType, variantValue);
    }

    private Dictionary<string, object?> DeserializeVariant(BinaryReader reader, AbiVariant variant)
    {
        var typeIndex = ReadVarUint32(reader);
        if (typeIndex >= variant.Types.Count)
            throw new InvalidOperationException($"Invalid variant type index: {typeIndex}");

        var variantType = variant.Types[(int)typeIndex];
        var value = DeserializeType(reader, variantType);

        return new Dictionary<string, object?>
        {
            [variantType] = value
        };
    }

    #endregion

    #region Write Helpers

    private static void WriteVarUint32(BinaryWriter writer, uint value)
    {
        while (value >= 0x80)
        {
            writer.Write((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        writer.Write((byte)value);
    }

    private static void WriteVarInt32(BinaryWriter writer, int value)
    {
        // ZigZag encoding
        var encoded = (uint)((value << 1) ^ (value >> 31));
        WriteVarUint32(writer, encoded);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarUint32(writer, (uint)bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteBytes(BinaryWriter writer, byte[] value)
    {
        WriteVarUint32(writer, (uint)value.Length);
        writer.Write(value);
    }

    private static void WriteInt128(BinaryWriter writer, Int128 value)
    {
        // Int128 as little-endian 16 bytes
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt128LittleEndian(bytes, value);
        writer.Write(bytes);
    }

    private static void WriteUInt128(BinaryWriter writer, UInt128 value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(bytes, value);
        writer.Write(bytes);
    }

    private static void WriteFloat128(BinaryWriter writer, object? value)
    {
        // Float128 as 16 bytes - usually passed as hex string or byte array
        var bytes = ConvertToBytes(value);
        if (bytes.Length != 16)
            throw new InvalidOperationException("float128 must be exactly 16 bytes");
        writer.Write(bytes);
    }

    private static void WriteChecksum(BinaryWriter writer, object? value, int length)
    {
        var bytes = value switch
        {
            byte[] b => b,
            string s => Convert.FromHexString(s),
            _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to checksum")
        };

        if (bytes.Length != length)
            throw new InvalidOperationException($"Checksum must be exactly {length} bytes");
        
        writer.Write(bytes);
    }

    private static void WritePublicKey(BinaryWriter writer, string key)
    {
        // Public key format: type (1 byte) + key data (33 bytes for K1/R1)
        if (key.StartsWith("PUB_K1_"))
        {
            writer.Write((byte)0); // K1 type
            var data = Base58CheckDecode(key[7..], "K1");
            writer.Write(data);
        }
        else if (key.StartsWith("PUB_R1_"))
        {
            writer.Write((byte)1); // R1 type
            var data = Base58CheckDecode(key[7..], "R1");
            writer.Write(data);
        }
        else if (key.StartsWith("EOS"))
        {
            writer.Write((byte)0); // Legacy K1 format
            var data = Base58CheckDecode(key[3..], "");
            writer.Write(data);
        }
        else
        {
            throw new InvalidOperationException($"Unknown public key format: {key}");
        }
    }

    private static void WriteSignature(BinaryWriter writer, string sig)
    {
        if (sig.StartsWith("SIG_K1_"))
        {
            writer.Write((byte)0); // K1 type
            var data = Base58CheckDecode(sig[7..], "K1");
            writer.Write(data);
        }
        else if (sig.StartsWith("SIG_R1_"))
        {
            writer.Write((byte)1); // R1 type
            var data = Base58CheckDecode(sig[7..], "R1");
            writer.Write(data);
        }
        else
        {
            throw new InvalidOperationException($"Unknown signature format: {sig}");
        }
    }

    private static void WriteSymbol(BinaryWriter writer, string symbol)
    {
        // Symbol format: "4,EOS" or just "EOS" with implicit precision
        byte precision;
        string code;

        if (symbol.Contains(','))
        {
            var parts = symbol.Split(',');
            precision = byte.Parse(parts[0]);
            code = parts[1];
        }
        else
        {
            precision = 4; // Default precision
            code = symbol;
        }

        // Symbol is stored as: precision (1 byte) + symbol code padded to 7 bytes
        writer.Write(precision);
        WriteSymbolCodeRaw(writer, code);
    }

    private static void WriteSymbolCode(BinaryWriter writer, string code)
    {
        // Symbol code as 8 bytes (padded)
        var bytes = new byte[8];
        var codeBytes = Encoding.ASCII.GetBytes(code.ToUpperInvariant());
        Array.Copy(codeBytes, bytes, Math.Min(codeBytes.Length, 7));
        writer.Write(bytes);
    }

    private static void WriteSymbolCodeRaw(BinaryWriter writer, string code)
    {
        // Write exactly 7 bytes of symbol code
        var bytes = new byte[7];
        var codeBytes = Encoding.ASCII.GetBytes(code.ToUpperInvariant());
        Array.Copy(codeBytes, bytes, Math.Min(codeBytes.Length, 7));
        writer.Write(bytes);
    }

    private static void WriteAsset(BinaryWriter writer, string asset)
    {
        // Asset format: "100.0000 EOS"
        var parts = asset.Trim().Split(' ');
        if (parts.Length != 2)
            throw new InvalidOperationException($"Invalid asset format: {asset}");

        var amountStr = parts[0];
        var symbolCode = parts[1];

        // Parse amount
        var dotIndex = amountStr.IndexOf('.');
        var precision = dotIndex >= 0 ? amountStr.Length - dotIndex - 1 : 0;
        var amountNormalized = amountStr.Replace(".", "");
        var amount = long.Parse(amountNormalized);

        // Write amount (int64)
        writer.Write(amount);
        
        // Write symbol (precision + code)
        writer.Write((byte)precision);
        WriteSymbolCodeRaw(writer, symbolCode);
    }

    private void WriteExtendedAsset(BinaryWriter writer, object? value)
    {
        var dict = ToDataDictionary(value);
        
        var quantity = dict.TryGetValue("quantity", out var q) ? q?.ToString() : "";
        var contract = dict.TryGetValue("contract", out var c) ? c?.ToString() : "";

        WriteAsset(writer, quantity ?? "0.0000 EOS");
        writer.Write(EosioSerializer.NameToUInt64(contract ?? ""));
    }

    private static void WriteTimePoint(BinaryWriter writer, object? value)
    {
        // time_point: microseconds since epoch as int64
        long microseconds = value switch
        {
            DateTime dt => ((DateTimeOffset)dt).ToUnixTimeMilliseconds() * 1000,
            DateTimeOffset dto => dto.ToUnixTimeMilliseconds() * 1000,
            long l => l,
            string s => (long)(DateTime.Parse(s) - DateTime.UnixEpoch).TotalMicroseconds,
            _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to time_point")
        };
        writer.Write(microseconds);
    }

    private static void WriteTimePointSec(BinaryWriter writer, object? value)
    {
        // time_point_sec: seconds since epoch as uint32
        uint seconds = value switch
        {
            DateTime dt => (uint)((DateTimeOffset)dt).ToUnixTimeSeconds(),
            DateTimeOffset dto => (uint)dto.ToUnixTimeSeconds(),
            uint u => u,
            int i => (uint)i,
            long l => (uint)l,
            string s => (uint)(DateTime.Parse(s) - DateTime.UnixEpoch).TotalSeconds,
            _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to time_point_sec")
        };
        writer.Write(seconds);
    }

    private static void WriteBlockTimestamp(BinaryWriter writer, object? value)
    {
        // block_timestamp_type: half-seconds since epoch (2018-06-01T00:00:00.000) as uint32
        var epoch = new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime dt = value switch
        {
            DateTime d => d,
            DateTimeOffset dto => dto.UtcDateTime,
            string s => DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind),
            _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to block_timestamp_type")
        };
        var halfSeconds = (uint)((dt - epoch).TotalSeconds * 2);
        writer.Write(halfSeconds);
    }

    #endregion

    #region Read Helpers

    private static uint ReadVarUint32(BinaryReader reader)
    {
        uint value = 0;
        int shift = 0;
        byte b;
        do
        {
            b = reader.ReadByte();
            value |= (uint)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return value;
    }

    private static int ReadVarInt32(BinaryReader reader)
    {
        var encoded = ReadVarUint32(reader);
        // ZigZag decoding
        return (int)((encoded >> 1) ^ (-(int)(encoded & 1)));
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = ReadVarUint32(reader);
        var bytes = reader.ReadBytes((int)length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] ReadBytes(BinaryReader reader)
    {
        var length = ReadVarUint32(reader);
        return reader.ReadBytes((int)length);
    }

    private static Int128 ReadInt128(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        return BinaryPrimitives.ReadInt128LittleEndian(bytes);
    }

    private static UInt128 ReadUInt128(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        return BinaryPrimitives.ReadUInt128LittleEndian(bytes);
    }

    private static byte[] ReadFloat128(BinaryReader reader)
    {
        return reader.ReadBytes(16);
    }

    private static string ReadChecksum(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ReadPublicKey(BinaryReader reader)
    {
        var keyType = reader.ReadByte();
        var data = reader.ReadBytes(33);
        
        return keyType switch
        {
            0 => "PUB_K1_" + Base58CheckEncode(data, "K1"),
            1 => "PUB_R1_" + Base58CheckEncode(data, "R1"),
            _ => throw new InvalidOperationException($"Unknown public key type: {keyType}")
        };
    }

    private static string ReadSignature(BinaryReader reader)
    {
        var sigType = reader.ReadByte();
        var data = reader.ReadBytes(65);
        
        return sigType switch
        {
            0 => "SIG_K1_" + Base58CheckEncode(data, "K1"),
            1 => "SIG_R1_" + Base58CheckEncode(data, "R1"),
            _ => throw new InvalidOperationException($"Unknown signature type: {sigType}")
        };
    }

    private static string ReadSymbol(BinaryReader reader)
    {
        var precision = reader.ReadByte();
        var codeBytes = reader.ReadBytes(7);
        var code = Encoding.ASCII.GetString(codeBytes).TrimEnd('\0');
        return $"{precision},{code}";
    }

    private static string ReadSymbolCode(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }

    private static string ReadAsset(BinaryReader reader)
    {
        var amount = reader.ReadInt64();
        var precision = reader.ReadByte();
        var codeBytes = reader.ReadBytes(7);
        var code = Encoding.ASCII.GetString(codeBytes).TrimEnd('\0');

        // Format amount with precision
        var amountStr = amount.ToString();
        if (precision > 0)
        {
            while (amountStr.Length <= precision)
                amountStr = "0" + amountStr;
            amountStr = amountStr.Insert(amountStr.Length - precision, ".");
        }

        return $"{amountStr} {code}";
    }

    private Dictionary<string, object?> ReadExtendedAsset(BinaryReader reader)
    {
        var quantity = ReadAsset(reader);
        var contract = EosioSerializer.UInt64ToName(reader.ReadUInt64());
        return new Dictionary<string, object?>
        {
            ["quantity"] = quantity,
            ["contract"] = contract
        };
    }

    private static DateTime ReadTimePoint(BinaryReader reader)
    {
        var microseconds = reader.ReadInt64();
        return DateTime.UnixEpoch.AddMicroseconds(microseconds);
    }

    private static DateTime ReadTimePointSec(BinaryReader reader)
    {
        var seconds = reader.ReadUInt32();
        return DateTime.UnixEpoch.AddSeconds(seconds);
    }

    private static DateTime ReadBlockTimestamp(BinaryReader reader)
    {
        var halfSeconds = reader.ReadUInt32();
        var epoch = new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(halfSeconds / 2.0);
    }

    #endregion

    #region Conversion Helpers

    private static Dictionary<string, object?> ToDataDictionary(object? data)
    {
        if (data == null)
            return new Dictionary<string, object?>();

        if (data is Dictionary<string, object?> dict)
            return dict;

        if (data is IDictionary<string, object> idict)
            return idict.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

        if (data is JsonElement je)
            return JsonElementToDict(je);

        // Try reflection for anonymous types and POCOs
        var type = data.GetType();
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var prop in type.GetProperties())
        {
            // Convert PascalCase to snake_case for ABI compatibility
            var name = ToSnakeCase(prop.Name);
            result[name] = prop.GetValue(data);
            // Also add the original name for direct matches
            result[prop.Name] = prop.GetValue(data);
        }

        return result;
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = JsonElementToObject(prop.Value);
        }

        return result;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDict(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static List<object?> ToArray(object? value)
    {
        if (value is IEnumerable<object?> enumerable)
            return enumerable.ToList();
        if (value is System.Collections.IEnumerable ie)
            return ie.Cast<object?>().ToList();
        throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to array");
    }

    private static bool ConvertToBool(object? value) => value switch
    {
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        string s => bool.Parse(s),
        JsonElement je when je.ValueKind == JsonValueKind.True => true,
        JsonElement je when je.ValueKind == JsonValueKind.False => false,
        _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to bool")
    };

    private static sbyte ConvertToSByte(object? value) => Convert.ToSByte(UnwrapNumber(value));
    private static byte ConvertToByte(object? value) => Convert.ToByte(UnwrapNumber(value));
    private static short ConvertToInt16(object? value) => Convert.ToInt16(UnwrapNumber(value));
    private static ushort ConvertToUInt16(object? value) => Convert.ToUInt16(UnwrapNumber(value));
    private static int ConvertToInt32(object? value) => Convert.ToInt32(UnwrapNumber(value));
    private static uint ConvertToUInt32(object? value) => Convert.ToUInt32(UnwrapNumber(value));
    private static long ConvertToInt64(object? value) => Convert.ToInt64(UnwrapNumber(value));
    private static ulong ConvertToUInt64(object? value) => Convert.ToUInt64(UnwrapNumber(value));
    private static float ConvertToFloat(object? value) => Convert.ToSingle(UnwrapNumber(value));
    private static double ConvertToDouble(object? value) => Convert.ToDouble(UnwrapNumber(value));

    private static object? UnwrapNumber(object? value)
    {
        if (value is JsonElement je)
        {
            if (je.TryGetInt64(out var l)) return l;
            if (je.TryGetDouble(out var d)) return d;
            if (je.ValueKind == JsonValueKind.String) return je.GetString();
        }
        return value;
    }

    private static Int128 ConvertToInt128(object? value)
    {
        if (value is string s)
            return Int128.Parse(s);
        if (value is byte[] b && b.Length == 16)
            return BinaryPrimitives.ReadInt128LittleEndian(b);
        return (Int128)ConvertToInt64(value);
    }

    private static UInt128 ConvertToUInt128(object? value)
    {
        if (value is string s)
            return UInt128.Parse(s);
        if (value is byte[] b && b.Length == 16)
            return BinaryPrimitives.ReadUInt128LittleEndian(b);
        return (UInt128)ConvertToUInt64(value);
    }

    private static byte[] ConvertToBytes(object? value) => value switch
    {
        byte[] b => b,
        string s => Convert.FromHexString(s),
        _ => throw new InvalidOperationException($"Cannot convert {value?.GetType().Name} to bytes")
    };

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }
            sb.Append(char.ToLower(c));
        }
        return sb.ToString();
    }

    #endregion

    #region Base58 Helpers

    private static byte[] Base58CheckDecode(string encoded, string suffix)
    {
        var decoded = Base58Decode(encoded);
        if (string.IsNullOrEmpty(suffix))
        {
            // Legacy format with SHA256 checksum
            return decoded[..^4];
        }
        
        // Modern format with RIPEMD160 checksum
        return decoded[..^4];
    }

    private static string Base58CheckEncode(byte[] data, string suffix)
    {
        var ripemd = new Org.BouncyCastle.Crypto.Digests.RipeMD160Digest();
        var suffixBytes = Encoding.UTF8.GetBytes(suffix);
        
        var toHash = new byte[data.Length + suffixBytes.Length];
        Array.Copy(data, 0, toHash, 0, data.Length);
        Array.Copy(suffixBytes, 0, toHash, data.Length, suffixBytes.Length);
        
        var hash = new byte[ripemd.GetDigestSize()];
        ripemd.BlockUpdate(toHash, 0, toHash.Length);
        ripemd.DoFinal(hash, 0);
        
        var withChecksum = new byte[data.Length + 4];
        Array.Copy(data, 0, withChecksum, 0, data.Length);
        Array.Copy(hash, 0, withChecksum, data.Length, 4);
        
        return Base58Encode(withChecksum);
    }

    private static byte[] Base58Decode(string encoded)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var value = Org.BouncyCastle.Math.BigInteger.Zero;

        foreach (var c in encoded)
        {
            var digit = alphabet.IndexOf(c);
            if (digit < 0)
                throw new FormatException($"Invalid Base58 character: {c}");
            value = value.Multiply(Org.BouncyCastle.Math.BigInteger.ValueOf(58))
                        .Add(Org.BouncyCastle.Math.BigInteger.ValueOf(digit));
        }

        var bytes = value.ToByteArray();
        
        // Remove leading zero byte if present (BigInteger sign byte)
        if (bytes.Length > 1 && bytes[0] == 0)
        {
            var trimmed = new byte[bytes.Length - 1];
            Array.Copy(bytes, 1, trimmed, 0, trimmed.Length);
            bytes = trimmed;
        }

        // Add leading zeros
        var leadingZeros = encoded.TakeWhile(c => c == '1').Count();
        if (leadingZeros > 0)
        {
            var withLeadingZeros = new byte[bytes.Length + leadingZeros];
            Array.Copy(bytes, 0, withLeadingZeros, leadingZeros, bytes.Length);
            bytes = withLeadingZeros;
        }

        return bytes;
    }

    private static string Base58Encode(byte[] data)
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var value = new Org.BouncyCastle.Math.BigInteger(1, data);
        var result = new List<char>();

        while (value.CompareTo(Org.BouncyCastle.Math.BigInteger.Zero) > 0)
        {
            var remainder = value.Mod(Org.BouncyCastle.Math.BigInteger.ValueOf(58));
            value = value.Divide(Org.BouncyCastle.Math.BigInteger.ValueOf(58));
            result.Insert(0, alphabet[remainder.IntValue]);
        }

        // Add leading zeros
        foreach (var b in data)
        {
            if (b != 0) break;
            result.Insert(0, '1');
        }

        return new string(result.ToArray());
    }

    #endregion
}

/// <summary>
/// Extension methods for ABI serialization
/// </summary>
public static class AbiSerializerExtensions
{
    /// <summary>
    /// Creates an ABI serializer from a blockchain client for a specific contract
    /// </summary>
    public static async Task<AbiSerializer?> GetAbiSerializerAsync(
        this Services.IAntelopeBlockchainClient client,
        string contractAccount,
        CancellationToken cancellationToken = default)
    {
        var abi = await client.GetAbiAsync(contractAccount, cancellationToken);
        return abi != null ? new AbiSerializer(abi) : null;
    }
}
