namespace SUS.EOS.EosioSigningRequest.Models;

/// <summary>
/// ESR request payload (transaction or action)
/// </summary>
public class EsrRequestPayload
{
    public bool IsTransaction => Transaction != null;
    public bool IsAction => Action != null;

    public object? Transaction { get; set; }
    public object? Action { get; set; }
}
