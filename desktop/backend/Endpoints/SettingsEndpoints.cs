using NeoWallet.Backend.Dto;
using NeoWallet.Backend.Services;

namespace NeoWallet.Backend.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/settings/autolock", (AppSettingsService settings) =>
            Results.Ok(new AutoLockSettingsDto(settings.AutoLockMinutes)));

        app.MapPost("/api/settings/autolock", (AutoLockSettingsDto req, AppSettingsService settings) =>
        {
            settings.AutoLockMinutes = req.TimeoutMinutes;
            return Results.Ok(new AutoLockSettingsDto(settings.AutoLockMinutes));
        });

        app.MapGet("/api/settings/app", (AppSettingsService settings) =>
            Results.Ok(new AppSettingsDto(settings.StartAtLogin, settings.MinimizeToTray)));

        app.MapPost("/api/settings/app", (AppSettingsDto req, AppSettingsService settings) =>
        {
            settings.StartAtLogin = req.StartAtLogin;
            settings.MinimizeToTray = req.MinimizeToTray;
            return Results.Ok(new AppSettingsDto(settings.StartAtLogin, settings.MinimizeToTray));
        });
    }
}
