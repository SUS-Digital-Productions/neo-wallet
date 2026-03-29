using System.Security.Cryptography;

namespace NeoWallet.Backend;

/// <summary>
/// Validates the startup-generated bearer token on every request.
/// The token is generated once at startup and printed to stdout so
/// the Tauri shell can inject it into the renderer.
/// </summary>
public sealed class BearerTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _token;

    public BearerTokenMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        // Allow an explicit token for development; otherwise generate one.
        _token = configuration["Auth:Token"]
            ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // Emit to stdout so the Tauri shell can read it.
        Console.WriteLine($"BACKEND_TOKEN={_token}");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var provided = auth["Bearer ".Length..];
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(provided),
                System.Text.Encoding.UTF8.GetBytes(_token)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
