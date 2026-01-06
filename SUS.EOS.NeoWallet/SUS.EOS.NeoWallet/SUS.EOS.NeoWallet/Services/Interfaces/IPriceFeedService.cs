namespace SUS.EOS.NeoWallet.Services.Interfaces;

/// <summary>
/// Price feed service for cryptocurrency prices
/// </summary>
public interface IPriceFeedService
{
    /// <summary>
    /// Get price for a symbol in USD
    /// </summary>
    Task<decimal?> GetPriceAsync(string symbol);

    /// <summary>
    /// Get prices for multiple symbols
    /// </summary>
    Task<Dictionary<string, decimal>> GetPricesAsync(params string[] symbols);

    /// <summary>
    /// Convert amount to USD value
    /// </summary>
    Task<decimal> ConvertToUsdAsync(string symbol, decimal amount);
}
