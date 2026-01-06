namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR Session Status Event Args
/// </summary>
public class EsrSessionStatusEventArgs : EventArgs
{
    public EsrSessionStatus Status { get; set; }
}
