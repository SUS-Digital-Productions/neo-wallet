namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR Signing Request Event Args
/// </summary>
public class EsrSigningRequestEventArgs : EventArgs
{
    public Esr? Request { get; set; }
    public string? RawPayload { get; set; }
    public EsrSession? Session { get; set; }
    public bool IsIdentityRequest { get; set; }
    public string? Callback { get; set; }
}
