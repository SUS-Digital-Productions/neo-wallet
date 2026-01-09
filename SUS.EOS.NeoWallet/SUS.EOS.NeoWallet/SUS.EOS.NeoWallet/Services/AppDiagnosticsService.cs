using System.Text;
using SUS.EOS.NeoWallet.Services.Interfaces;

namespace SUS.EOS.NeoWallet.Services;

public class AppDiagnosticsService : IAppDiagnosticsService
{
    private readonly object _sync = new();
    private readonly LinkedList<string> _entries = new();
    private readonly int _maxEntries = 5000;

    public void Log(string source, string message)
    {
        var now = DateTime.UtcNow.ToString("o");
        var entry = $"[{now}] {source}: {message}";
        lock (_sync)
        {
            _entries.AddLast(entry);
            if (_entries.Count > _maxEntries)
                _entries.RemoveFirst();
        }
    }

    public IReadOnlyList<string> GetEntries()
    {
        lock (_sync)
        {
            return _entries.Reverse().ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
        }
    }

    public async Task<string> ExportAsync(string filePath)
    {
        // Ensure parent dir exists
        var dir = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        lock (_sync)
        {
            foreach (var e in _entries)
            {
                sb.AppendLine(e);
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
        return filePath;
    }
}