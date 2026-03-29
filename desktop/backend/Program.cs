using NeoWallet.Backend;
using NeoWallet.Backend.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackendServices();

var app = builder.Build();

app.UseMiddleware<BearerTokenMiddleware>();

app.MapHealthEndpoints();
app.MapWalletEndpoints();
app.MapAccountEndpoints();
app.MapNetworkEndpoints();
app.MapBalanceEndpoints();
app.MapTransferEndpoints();
app.MapEsrEndpoints();

var url = $"http://localhost:{app.Configuration.GetValue("Port", 5199)}";
System.Diagnostics.Trace.WriteLine($"[BACKEND] Starting on {url}");
app.Run(url);
