# neo-wallet

Desktop Antelope wallet built with Tauri + React for the UI and a local .NET backend for wallet logic, signing, and blockchain access.

## Repository Layout

| Directory | Purpose |
|-----------|---------|
| `desktop/app/` | Tauri 2 + React + Vite renderer shell |
| `desktop/backend/` | ASP.NET Core sidecar (wallet storage, signing, ESR, chain RPC) |
| `SUS.EOS.NeoWallet/SUS.EOS.Sharp/` | Reusable Antelope blockchain client library |
| `SUS.EOS.NeoWallet/SUS.EOS.EosioSigningRequest/` | ESR protocol parsing, signing-request models, session services |
| `SUS.EOS.NeoWallet/SUS.EOS.Sharp.Tests/` | xUnit tests for the libraries |
| `docs/` | Architecture and API documentation |

## Quick Start

```powershell
# Build all .NET projects
dotnet build SUS.EOS.NeoWallet/SUS.EOS.NeoWallet.slnx

# Run the backend
cd desktop/backend
dotnet run

# In another terminal — run the React dev server
cd desktop/app
npm install
npm run dev
```

See [desktop/README.md](desktop/README.md) for full development bootstrap, Tauri setup, and production build instructions.

## Documentation

- [Desktop Shell README](desktop/README.md)
- [Tauri React Desktop Integration](docs/tauri-react-desktop.md)
- [Local Backend API Outline](docs/local-backend-api.md)
- [SUS.EOS.Sharp README](SUS.EOS.NeoWallet/SUS.EOS.Sharp/README.md)
- [NuGet Package Notes](NUGET_PACKAGE.md)

## Status

- .NET backend libraries (`SUS.EOS.Sharp`, `SUS.EOS.EosioSigningRequest`) — stable
- Desktop backend sidecar (`NeoWallet.Backend`) — scaffolded with stub endpoints
- Tauri + React shell (`desktop/app/`) — scaffolded with API client and dashboard page
- Wiring real blockchain operations into endpoints — in progress

MIT License - See [LICENSE](LICENSE) file for details.