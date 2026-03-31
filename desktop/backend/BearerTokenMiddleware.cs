using System.Security.Cryptography;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend;

/// <summary>
/// Holds the backend bearer token generated at startup.
/// Registered as a singleton so endpoints can include the token in unlock responses.
/// </summary>
public sealed class BackendTokenHolder
{
    public string Token { get; }

    public BackendTokenHolder(IConfiguration configuration)
    {
        Token = configuration["Auth:Token"]
            ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Console.WriteLine($"BACKEND_TOKEN={Token}");
        System.Diagnostics.Trace.WriteLine($"[AUTH] Token={Token}");
    }
}

/// <summary>
/// Validates the startup-generated bearer token on every request.
/// The token is generated once at startup and printed to stdout so
/// the Tauri shell can inject it into the renderer.
/// </summary>
public sealed class BearerTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _token;

    public BearerTokenMiddleware(RequestDelegate next, BackendTokenHolder holder)
    {
        _next = next;
        _token = holder.Token;
    }

    // Endpoints that must work before the renderer has a token.
    private static readonly string[] OpenPaths =
    [
        "/api/health",
        "/api/wallet/create",
        "/api/wallet/unlock",
        "/api/esr/incoming",
    ];

    public async Task InvokeAsync(HttpContext context, AutoLockService autoLock)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for static files and open API paths.
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || OpenPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Accept token from Authorization header or query parameter (for WebSocket connections).
        var auth = context.Request.Headers.Authorization.ToString();
        string? provided = null;

        if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            provided = auth["Bearer ".Length..];
        }
        else if (context.Request.Query.TryGetValue("token", out var qToken) && !string.IsNullOrEmpty(qToken))
        {
            provided = qToken.ToString();
        }

        if (string.IsNullOrEmpty(provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(provided),
                System.Text.Encoding.UTF8.GetBytes(_token)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Track activity for auto-lock
        autoLock.Touch();

        await _next(context);
    }
}
