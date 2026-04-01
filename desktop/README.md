# Desktop & Mobile Shell

This directory contains the Neo Wallet application shell and backend:

| Directory | Technology | Purpose |
|-----------|-----------|---------|
| `app/` | Tauri 2 + React + Vite | Native shell (desktop & mobile) and renderer UI |
| `backend/` | ASP.NET Core (net10.0) | Desktop sidecar: wallet storage, signing, ESR, chain RPC |

## Architecture

### Desktop

```
React (Vite dev server / Tauri webview)
    ↓  localhost HTTP + bearer token
.NET sidecar (NeoWallet.Backend) on port 5199
    ↓  project references
SUS.EOS.Sharp  /  SUS.EOS.EosioSigningRequest
```

The Tauri shell spawns the .NET backend as a sidecar process, manages the system tray icon,
and handles deep links (`esr://` URIs).

### Mobile (Android / iOS)

```
React (bundled in Tauri Mobile webview)
    ↓  localhost HTTP + bearer token
Embedded Rust HTTP backend (axum) on port 5199
    ↓  native Rust crypto
AES-256-CBC wallet + PBKDF2 key derivation
```

On mobile, the .NET sidecar cannot run. Instead, the Tauri Rust layer embeds an **axum HTTP
server** that implements the same REST API the React frontend expects. The wallet file format
(AES-256-CBC with PBKDF2-SHA256) is byte-compatible with the desktop .NET backend, so wallet
files can be imported/exported across platforms.

**Mobile implementation status:**
- ✅ Wallet create / unlock / lock / export / import
- ✅ Account & key management (CRUD)
- ✅ Network switching
- ✅ Bearer token auth
- ⏳ Balance lookups (returns empty — requires `reqwest` RPC integration)
- ⏳ Transaction signing & broadcast (returns 501 — requires `k256` signing)
- ⏳ ESR protocol (stubs return 501)
- ⏳ Account lookup by private key (needs secp256k1 derivation)

## Prerequisites

### .NET (desktop backend)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Node.js
- Node.js 24+ and npm

### Rust / Tauri
- [Rust](https://rustup.rs/) (stable toolchain)
- Tauri 2 system dependencies — see [Tauri prerequisites](https://v2.tauri.app/start/prerequisites/)
  - Windows: Visual Studio Build Tools with "Desktop development with C++"
  - macOS: Xcode command-line tools
  - Linux: `libwebkit2gtk-4.1-dev`, `build-essential`, `libssl-dev`, etc.

### Android (optional)
- Android SDK (API 24+)
- Android NDK
- Add Rust Android targets: `rustup target add aarch64-linux-android armv7-linux-androideabi x86_64-linux-android i686-linux-android`

### iOS (optional)
- Xcode 15+
- Add Rust iOS targets: `rustup target add aarch64-apple-ios aarch64-apple-ios-sim x86_64-apple-ios`

## Quick Start — Desktop

### 1. Start the .NET backend

```powershell
cd desktop/backend
dotnet run
```

The backend starts on `http://localhost:5199` and prints `BACKEND_TOKEN=<hex>` to stdout.

### 2. Start the React dev server

```powershell
cd desktop/app
npm install
npm run dev
```

Vite serves on `http://localhost:1420`. Set `VITE_BACKEND_URL=http://localhost:5199` for
the frontend to reach the backend during development.

### 3. Run through Tauri (desktop)

```powershell
cd desktop/app
npm run tauri dev
```

Compiles the Rust shell, starts Vite, and opens the native window. The Tauri shell spawns
the .NET sidecar automatically (requires backend published first — see production build).

## Quick Start — Mobile

### Android

```powershell
cd desktop/app
npm install
npm run tauri:android:init    # one-time: generates gen/android/
npm run tauri:android:dev     # dev build + launch on connected device/emulator
```

### iOS

```powershell
cd desktop/app
npm install
npm run tauri:ios:init        # one-time: generates gen/apple/
npm run tauri:ios:dev         # dev build + launch on simulator
```

On mobile, the embedded Rust backend starts automatically — no .NET sidecar needed.

## Building for Production

### Desktop

1. Publish the .NET backend as a sidecar:

```powershell
cd desktop/backend
dotnet publish -c Release -r win-x64 --self-contained -o ../app/src-tauri/sidecar
# Rename to include target triple (required by Tauri):
Rename-Item ../app/src-tauri/sidecar/NeoWallet.Backend.exe NeoWallet.Backend-x86_64-pc-windows-msvc.exe
```

2. Build the Tauri bundle:

```powershell
cd desktop/app
npm run tauri build
```

### Mobile

```powershell
cd desktop/app
npm run tauri:android:build   # produces APK in src-tauri/gen/android/
npm run tauri:ios:build       # produces Xcode archive in src-tauri/gen/apple/
```

## Backend API

All endpoints require `Authorization: Bearer <token>` except where noted.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/health` | No | Backend readiness |
| POST | `/api/wallet/create` | No | Create new wallet |
| POST | `/api/wallet/unlock` | No | Unlock with password → returns token |
| POST | `/api/wallet/lock` | Yes | Lock wallet |
| GET | `/api/wallet/summary` | Yes | Active network, account, listener status |
| GET | `/api/wallet/export` | Yes | Download encrypted wallet file |
| POST | `/api/wallet/import` | No | Import wallet from file |
| GET | `/api/accounts` | Yes | List accounts |
| POST | `/api/accounts/active` | Yes | Set active account |
| POST | `/api/accounts/import` | Yes | Import account(s) with private key |
| POST | `/api/accounts/remove` | Yes | Remove account |
| POST | `/api/accounts/private-key` | Yes | Get private key for account |
| POST | `/api/accounts/lookup` | Yes | Look up accounts by private key |
| GET | `/api/keys` | Yes | List standalone keys |
| POST | `/api/keys` | Yes | Add key |
| POST | `/api/keys/remove` | Yes | Remove key |
| GET | `/api/networks` | Yes | List supported networks |
| POST | `/api/networks/active` | Yes | Set active network |
| GET | `/api/balances?account=…&chainId=…` | Yes | Token balances |
| POST | `/api/transfers` | Yes | Build, sign, broadcast transfer |
| POST | `/api/esr/parse` | Yes | Parse ESR URI |
| POST | `/api/esr/approve` | Yes | Approve ESR request |
| POST | `/api/esr/reject` | Yes | Reject ESR request |
| POST | `/api/esr/incoming` | No | Receive incoming ESR |

See [local-backend-api.md](../docs/local-backend-api.md) for full request/response shapes.

## Project Structure

```
desktop/
├── app/
│   ├── package.json
│   ├── vite.config.ts
│   ├── src/
│   │   ├── main.tsx
│   │   ├── App.tsx              # Router + AuthGate
│   │   ├── Layout.tsx           # Shell layout with sidebar
│   │   ├── api/
│   │   │   ├── types.ts         # Shared DTO types
│   │   │   ├── client.ts        # Typed fetch wrapper
│   │   │   ├── hooks.ts         # React Query hooks
│   │   │   └── useEsrEvents.ts  # WebSocket ESR events
│   │   └── pages/               # Route pages
│   └── src-tauri/
│       ├── Cargo.toml
│       ├── tauri.conf.json
│       ├── capabilities/
│       │   └── default.json
│       └── src/
│           ├── main.rs           # Desktop entry point
│           ├── lib.rs            # Shared setup (desktop + mobile)
│           └── mobile_backend/   # Mobile-only embedded HTTP backend
│               ├── mod.rs        # Server bootstrap
│               ├── crypto.rs     # AES-256-CBC + PBKDF2 wallet crypto
│               ├── routes.rs     # axum HTTP endpoints
│               └── state.rs      # Shared wallet state
└── backend/
    ├── NeoWallet.Backend.csproj
    ├── Program.cs
    ├── BearerTokenMiddleware.cs
    ├── ServiceRegistration.cs
    ├── Dto/
    ├── Endpoints/
    └── Services/
```
