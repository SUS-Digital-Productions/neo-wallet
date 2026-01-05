using System.Globalization;
using System.Text.RegularExpressions;

namespace SUS.EOS.Sharp.Models;

/// <summary>
/// Represents an EOS asset (amount + symbol)
/// </summary>
public sealed partial record Asset
{
    private static readonly Regex AssetRegex = GenerateAssetRegex();

    /// <summary>
    /// Asset amount (e.g., 100.0000)
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Number of decimal places
    /// </summary>
    public required byte Precision { get; init; }

    /// <summary>
    /// Asset symbol (e.g., "EOS", "GAS")
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the asset as a formatted string (e.g., "100.0000 EOS")
    /// </summary>
    public override string ToString()
    {
        var format = $"F{Precision}";
        return $"{Amount.ToString(format, CultureInfo.InvariantCulture)} {Symbol}";
    }

    /// <summary>
    /// Parses an asset string (e.g., "100.0000 EOS")
    /// </summary>
    public static Asset Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Asset value cannot be null or empty", nameof(value));

        var match = AssetRegex.Match(value.Trim());
        if (!match.Success)
            throw new FormatException($"Invalid asset format: {value}");

        var amountStr = match.Groups["amount"].Value;
        var symbol = match.Groups["symbol"].Value;

        // Calculate precision from decimal places
        var decimalIndex = amountStr.IndexOf('.');
        var precision = decimalIndex >= 0 
            ? (byte)(amountStr.Length - decimalIndex - 1) 
            : (byte)0;

        var amount = decimal.Parse(amountStr, CultureInfo.InvariantCulture);

        return new Asset
        {
            Amount = amount,
            Precision = precision,
            Symbol = symbol
        };
    }

    /// <summary>
    /// Tries to parse an asset string
    /// </summary>
    public static bool TryParse(string value, out Asset? asset)
    {
        try
        {
            asset = Parse(value);
            return true;
        }
        catch
        {
            asset = null;
            return false;
        }
    }

    [GeneratedRegex(@"^(?<amount>-?\d+(?:\.\d+)?)\s+(?<symbol>[A-Z]{1,7})$", RegexOptions.Compiled)]
    private static partial Regex GenerateAssetRegex();
}
