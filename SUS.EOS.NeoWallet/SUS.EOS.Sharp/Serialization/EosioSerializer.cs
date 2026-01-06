using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SUS.EOS.Sharp.Models;

namespace SUS.EOS.Sharp.Serialization;

/// <summary>
/// EOSIO ABI binary serialization
/// </summary>
public static class EosioSerializer
{
    /// <summary>
    /// Serializes a transaction to binary format
    /// </summary>
    public static byte[] SerializeTransaction<T>(EosioTransaction<T> transaction)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Expiration (uint32 - seconds since epoch)
        // Parse as UTC directly - the expiration string is already in UTC format
        var expiration = DateTime.Parse(
            transaction.Expiration, 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondsSinceEpoch = (uint)(expiration - epochStart).TotalSeconds;
        writer.Write(secondsSinceEpoch);

        // Reference block number (uint16)
        System.Diagnostics.Trace.WriteLine($"[SERIALIZER] RefBlockNum: {transaction.RefBlockNum}");
        writer.Write(transaction.RefBlockNum);

        // Reference block prefix (uint32)
        System.Diagnostics.Trace.WriteLine($"[SERIALIZER] RefBlockPrefix: {transaction.RefBlockPrefix}");
        writer.Write(transaction.RefBlockPrefix);

        // Max net usage words (varint)
        WriteVarUint32(writer, transaction.MaxNetUsageWords);

        // Max CPU usage ms (uint8)
        writer.Write(transaction.MaxCpuUsageMs);

        // Delay sec (varint)
        WriteVarUint32(writer, transaction.DelaySec);

        // Context free actions (array)
        WriteVarUint32(writer, (uint)transaction.ContextFreeActions.Count);
        foreach (var action in transaction.ContextFreeActions)
        {
            SerializeAction(writer, action);
        }

        // Actions (array)
        WriteVarUint32(writer, (uint)transaction.Actions.Count);
        foreach (var action in transaction.Actions)
        {
            SerializeAction(writer, action);
        }

        // Transaction extensions (array)
        WriteVarUint32(writer, (uint)transaction.TransactionExtensions.Count);

        return ms.ToArray();
    }

    /// <summary>
    /// Serializes a transaction with ABI-based action data serialization
    /// </summary>
    /// <param name="transaction">The transaction to serialize</param>
    /// <param name="abiProvider">Function that returns the ABI for a contract account</param>
    public static byte[] SerializeTransactionWithAbi<T>(
        EosioTransaction<T> transaction, 
        Func<string, AbiDefinition?> abiProvider)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Transaction header (same as before)
        var expiration = DateTime.Parse(
            transaction.Expiration, 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondsSinceEpoch = (uint)(expiration - epochStart).TotalSeconds;
        writer.Write(secondsSinceEpoch);
        writer.Write(transaction.RefBlockNum);
        writer.Write(transaction.RefBlockPrefix);
        WriteVarUint32(writer, transaction.MaxNetUsageWords);
        writer.Write(transaction.MaxCpuUsageMs);
        WriteVarUint32(writer, transaction.DelaySec);

        // Context free actions
        WriteVarUint32(writer, (uint)transaction.ContextFreeActions.Count);
        foreach (var action in transaction.ContextFreeActions)
        {
            SerializeActionWithAbi(writer, action, abiProvider);
        }

        // Actions
        WriteVarUint32(writer, (uint)transaction.Actions.Count);
        foreach (var action in transaction.Actions)
        {
            SerializeActionWithAbi(writer, action, abiProvider);
        }

        // Transaction extensions
        WriteVarUint32(writer, (uint)transaction.TransactionExtensions.Count);

        return ms.ToArray();
    }

    /// <summary>
    /// Serializes an action with ABI-based data serialization
    /// </summary>
    private static void SerializeActionWithAbi<T>(
        BinaryWriter writer, 
        EosioAction<T> action, 
        Func<string, AbiDefinition?> abiProvider)
    {
        // Account name (uint64)
        writer.Write(NameToUInt64(action.Account));

        // Action name (uint64)
        writer.Write(NameToUInt64(action.Name));

        // Authorization array
        WriteVarUint32(writer, (uint)action.Authorization.Count);
        foreach (var auth in action.Authorization)
        {
            writer.Write(NameToUInt64(auth.Actor));
            writer.Write(NameToUInt64(auth.Permission));
        }

        // Data (serialized based on ABI)
        byte[] dataBytes;
        if (action.IsBinaryData && action.Data is byte[] binaryData)
        {
            // Use pre-serialized binary data directly
            dataBytes = binaryData;
        }
        else
        {
            // Try to get ABI and serialize with it
            var abi = abiProvider(action.Account);
            if (abi != null && action.Data != null)
            {
                var serializer = new AbiSerializer(abi);
                dataBytes = serializer.SerializeActionData(action.Name, action.Data);
            }
            else
            {
                // Fall back to JSON serialization
                dataBytes = SerializeActionData(action.Data);
            }
        }
        WriteVarUint32(writer, (uint)dataBytes.Length);
        writer.Write(dataBytes);
    }

    /// <summary>
    /// Serializes an action
    /// </summary>
    private static void SerializeAction<T>(BinaryWriter writer, EosioAction<T> action)
    {
        // Account name (uint64)
        writer.Write(NameToUInt64(action.Account));

        // Action name (uint64)
        writer.Write(NameToUInt64(action.Name));

        // Authorization array
        WriteVarUint32(writer, (uint)action.Authorization.Count);
        foreach (var auth in action.Authorization)
        {
            writer.Write(NameToUInt64(auth.Actor));
            writer.Write(NameToUInt64(auth.Permission));
        }

        // Data (serialized as bytes)
        byte[] dataBytes;
        if (action.IsBinaryData && action.Data is byte[] binaryData)
        {
            // Use pre-serialized binary data directly
            dataBytes = binaryData;
        }
        else
        {
            dataBytes = SerializeActionData(action.Data);
        }
        WriteVarUint32(writer, (uint)dataBytes.Length);
        writer.Write(dataBytes);
    }
    
    /// <summary>
    /// Serializes a transaction with binary action data
    /// </summary>
    public static byte[] SerializeTransactionWithBinaryData(EosioTransaction<byte[]> transaction)
    {
        // This uses the same serialization, the IsBinaryData flag handles the difference
        return SerializeTransaction(transaction);
    }

    /// <summary>
    /// Serializes action data (simple JSON to bytes for now)
    /// </summary>
    private static byte[] SerializeActionData(object? data)
    {
        if (data == null)
            return Array.Empty<byte>();

        // For simple cases, serialize as JSON bytes
        // In production, this should use proper ABI serialization based on contract ABI
        var json = JsonSerializer.Serialize(data);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Converts EOSIO name to uint64
    /// </summary>
    public static ulong NameToUInt64(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        ulong value = 0;
        int len = Math.Min(name.Length, 13);

        for (int i = 0; i < len; i++)
        {
            ulong c;
            if (i < 12)
            {
                // Characters 0-11 use 5 bits each
                c = CharToSymbol(name[i]);
                // Bits for char i are at position: 64 - 5*(i+1) to 64 - 5*i - 1
                // Which means shift the 5-bit value left by: 64 - 5*(i+1) = 59 - 5*i
                int shift = i < 12 ? (59 - 5 * i) : 0;
                value |= (c & 0x1F) << shift;
            }
            else
            {
                // Character 12 (13th) uses only 4 bits in positions 3-0
                c = CharToSymbol(name[i]);
                value |= c & 0x0F;
            }
        }

        return value;
    }

    /// <summary>
    /// Converts character to EOSIO symbol
    /// </summary>
    private static ulong CharToSymbol(char c)
    {
        if (c >= 'a' && c <= 'z')
            return (ulong)(c - 'a' + 6);
        if (c >= '1' && c <= '5')
            return (ulong)(c - '1' + 1);
        return 0;
    }

    /// <summary>
    /// Converts uint64 back to EOSIO name (for debugging)
    /// </summary>
    public static string UInt64ToName(ulong value)
    {
        const string charmap = ".12345abcdefghijklmnopqrstuvwxyz";
        var chars = new char[13];
        
        // First 12 characters: extract 5 bits each from MSB to LSB
        // The first character is in bits 59-63, second in 54-58, etc.
        for (int i = 0; i < 12; i++)
        {
            // Shift right to bring the relevant 5 bits to position 0-4
            // For i=0, we want bits 59-63, so shift right by 64-5=59
            // For i=1, we want bits 54-58, so shift right by 64-10=54
            // General: shift by 64 - 5*(i+1) = 59 - 5*i
            int shift = 59 - 5 * i;
            int idx = (int)((value >> shift) & 0x1F);
            chars[i] = charmap[idx];
        }
        
        // 13th character uses only 4 bits (bits 0-3)
        int lastIdx = (int)(value & 0x0F);
        chars[12] = charmap[lastIdx];
        
        // Trim trailing dots
        return new string(chars).TrimEnd('.');
    }

    /// <summary>
    /// Writes variable-length unsigned 32-bit integer
    /// </summary>
    private static void WriteVarUint32(BinaryWriter writer, uint value)
    {
        while (value >= 0x80)
        {
            writer.Write((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        writer.Write((byte)value);
    }

    /// <summary>
    /// Creates signing data (chainId + serializedTransaction + 32 zeros)
    /// </summary>
    public static byte[] CreateSigningData(string chainId, byte[] serializedTransaction)
    {
        var chainIdBytes = HexStringToBytes(chainId);
        var contextFreeData = new byte[32]; // 32 zeros for no context-free data

        var signingData = new byte[chainIdBytes.Length + serializedTransaction.Length + contextFreeData.Length];
        Array.Copy(chainIdBytes, 0, signingData, 0, chainIdBytes.Length);
        Array.Copy(serializedTransaction, 0, signingData, chainIdBytes.Length, serializedTransaction.Length);
        Array.Copy(contextFreeData, 0, signingData, chainIdBytes.Length + serializedTransaction.Length, contextFreeData.Length);

        return signingData;
    }

    /// <summary>
    /// Computes SHA256 hash
    /// </summary>
    public static byte[] Sha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }

    /// <summary>
    /// Converts hex string to bytes
    /// </summary>
    public static byte[] HexStringToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Converts bytes to hex string
    /// </summary>
    public static string BytesToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}

/// <summary>
/// EOSIO transaction model for serialization
/// </summary>
public record EosioTransaction<T>
{
    public string Expiration { get; init; } = string.Empty;
    public ushort RefBlockNum { get; init; }
    public uint RefBlockPrefix { get; init; }
    public uint MaxNetUsageWords { get; init; }
    public byte MaxCpuUsageMs { get; init; }
    public uint DelaySec { get; init; }
    public List<EosioAction<T>> ContextFreeActions { get; init; } = new();
    public List<EosioAction<T>> Actions { get; init; } = new();
    public List<object> TransactionExtensions { get; init; } = new();
}

/// <summary>
/// EOSIO action model
/// </summary>
public record EosioAction<T>
{
    public string Account { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<EosioAuthorization> Authorization { get; init; } = new();
    public T? Data { get; init; }
    
    /// <summary>
    /// When true, Data is treated as pre-serialized binary (byte[])
    /// </summary>
    public bool IsBinaryData { get; init; } = false;
}

/// <summary>
/// EOSIO authorization model
/// </summary>
public record EosioAuthorization
{
    public string Actor { get; init; } = string.Empty;
    public string Permission { get; init; } = string.Empty;
}
