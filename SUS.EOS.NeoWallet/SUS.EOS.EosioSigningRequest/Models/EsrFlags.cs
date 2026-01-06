namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR request flags
/// </summary>
[Flags]
public enum EsrFlags : byte
{
    None = 0,
    Broadcast = 1 << 0,      // Request should be broadcast by wallet
    Background = 1 << 1       // Request can be processed in background
}
