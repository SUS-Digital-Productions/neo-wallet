namespace SUS.EOS.NeoWallet.Services.Interfaces;

public interface IAppDiagnosticsService
{
    void Log(string source, string message);
    IReadOnlyList<string> GetEntries();
    void Clear();
    Task<string> ExportAsync(string filePath);
}