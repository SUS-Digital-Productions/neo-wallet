# Desktop Shell

This directory contains the two halves of the Neo Wallet desktop application:

| Directory | Technology | Purpose |
|-----------|-----------|---------|
| `app/` | Tauri 2 + React + Vite | Native window shell and renderer UI |
| `backend/` | ASP.NET Core (net10.0) | Local sidecar: wallet storage, signing, ESR, chain RPC |

## Architecture

```
React (Vite dev server / Tauri webview)
    ↓  localhost HTTP + bearer token
.NET sidecar (NeoWallet.Backend)
    ↓  project references
SUS.EOS.Sharp  /  SUS.EOS.EosioSigningRequest
```

Private keys, signing, wallet encryption, and ESR session handling stay in .NET.
React is the presentation layer only.

## Prerequisites

### .NET
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Node.js
- Node.js 20+ and npm (or pnpm/yarn)

### Rust / Tauri
- [Rust](https://rustup.rs/) (stable toolchain)
- Tauri 2 system dependencies — see [Tauri prerequisites](https://v2.tauri.app/start/prerequisites/)
  - Windows: Visual Studio Build Tools with the "Desktop development with C++" workload
  - macOS: Xcode command-line tools
  - Linux: `libwebkit2gtk-4.1-dev`, `build-essential`, `libssl-dev`, etc.

## Quick Start (development)

### 1. Start the .NET backend

```powershell
cd desktop/backend
dotnet run
```

The backend starts on `http://localhost:5199` and prints `BACKEND_TOKEN=<hex>` to stdout.
Use this token as the `Authorization: Bearer <token>` header when calling the API, or
set the `VITE_BACKEND_URL` env var and pass the token to the React app via `sessionStorage`.

### 2. Start the React dev server

```powershell
cd desktop/app
npm install
npm run dev
```

Vite will serve on `http://localhost:1420` with HMR.

### 3. (Optional) Run through Tauri

```powershell
cd desktop/app
npm run tauri dev
```

This compiles the Rust shell, starts the Vite dev server automatically, and opens the native window.
The Tauri shell will also attempt to spawn the .NET backend as a sidecar (requires the backend to be published first — see below).

## Building for production

### Publish the .NET backend

```powershell
cd desktop/backend
dotnet publish -c Release -r win-x64 --self-contained -o ../app/src-tauri/sidecar
```

Rename the output to match the sidecar name in `tauri.conf.json`:

```powershell
Rename-Item ../app/src-tauri/sidecar/NeoWallet.Backend.exe NeoWallet.Backend-x86_64-pc-windows-msvc.exe
```

Tauri requires the sidecar binary name to include the target triple.

### Build the Tauri bundle

```powershell
cd desktop/app
npm run tauri build
```

The installer / MSIX will be in `src-tauri/target/release/bundle/`.

## Backend API

All endpoints require the `Authorization: Bearer <token>` header.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Backend readiness |
| GET | `/api/wallet/summary` | Active network, account, listener status |
| POST | `/api/wallet/unlock` | Unlock wallet with password |
| GET | `/api/accounts` | List accounts |
| POST | `/api/accounts/active` | Set active account |
| GET | `/api/networks` | List supported networks |
| POST | `/api/networks/active` | Set active network |
| GET | `/api/balances?account=…&chainId=…` | Token balances |
| POST | `/api/transfers` | Build, sign, broadcast a transfer |
| POST | `/api/esr/parse` | Parse an ESR URI |
| POST | `/api/esr/approve` | Approve and sign an ESR request |
| POST | `/api/esr/reject` | Reject an ESR request |

See [local-backend-api.md](../docs/local-backend-api.md) for full request/response shapes.

## Project structure

```
desktop/
├── app/
│   ├── index.html
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── src/
│   │   ├── main.tsx           # React entry
│   │   ├── App.tsx            # Router setup
│   │   ├── Layout.tsx         # Shell layout
│   │   ├── index.css          # Global styles
│   │   ├── api/
│   │   │   ├── types.ts       # Shared DTO types (mirrors backend DTOs)
│   │   │   └── client.ts      # Typed fetch wrapper
│   │   └── pages/
│   │       └── Dashboard.tsx   # Initial page
│   └── src-tauri/
│       ├── Cargo.toml
│       ├── tauri.conf.json
│       ├── capabilities/
│       │   └── default.json
│       └── src/
│           ├── main.rs
│           └── lib.rs          # Sidecar lifecycle
└── backend/
    ├── NeoWallet.Backend.csproj
    ├── Program.cs              # Minimal API host
    ├── BearerTokenMiddleware.cs
    ├── ServiceRegistration.cs
    ├── Dto/                    # Frontend-facing DTOs
    │   ├── AccountDto.cs
    │   ├── BalanceDto.cs
    │   ├── EsrDto.cs
    │   ├── NetworkDto.cs
    │   ├── TransferDto.cs
    │   └── WalletDto.cs
    ├── Endpoints/              # Minimal API endpoint groups
    │   ├── AccountEndpoints.cs
    │   ├── BalanceEndpoints.cs
    │   ├── EsrEndpoints.cs
    │   ├── HealthEndpoints.cs
    │   ├── NetworkEndpoints.cs
    │   ├── TransferEndpoints.cs
    │   └── WalletEndpoints.cs
    └── Services/
        ├── IWalletStateService.cs
        └── WalletStateService.cs   # Stub — wire real logic here
```

## Next steps

- Wire `BalanceEndpoints` to `SUS.EOS.Sharp` chain RPC calls
- Wire `TransferEndpoints` to `AntelopeTransactionService` for real signing + broadcast
- Wire `EsrEndpoints` to `EsrService` for real ESR parsing
- Implement encrypted wallet storage in `WalletStateService`
- Add SSE or WebSocket push for incoming ESR requests
- Build out React pages (Send, Receive, ESR approval, Settings)
