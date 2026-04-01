# How to Run Neo Wallet

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| **.NET SDK** | 10.0+ | Backend + static file server |
| **Node.js** | 24+ | Build the React frontend |
| **npm** | 10+ | Package management |

---

## Quick Start

Everything runs as **one process on one port** — the .NET backend serves the React frontend.

### 1. Build the Frontend

```bash
cd desktop/app
npm install
npm run build
```

This creates `desktop/app/dist/` with the production React bundle.

### 2. Run the Backend

```bash
cd desktop/backend
dotnet run
```

The app opens in a **native window** (via WebView2). No browser needed.

Use `dotnet run -- --browser` to open in your web browser instead.

### 3. First-Time Setup

1. The wallet opens — you'll see the **Unlock** page
2. Since no wallet exists yet, you'll see "Create a password to secure your wallet"
3. Enter a password (min 8 characters) and confirm it
4. Click **Create Wallet**
5. Navigate to **Import**, enter your private key (WIF: `5K...` or `PVT_K1_...`)
6. The app searches all chains (WAX, EOS, Telos) for linked accounts
7. Select the accounts you want to import, click **Import**
8. Done — you're on the Dashboard

### 4. Returning Users

1. Launch the app — enter your password and click **Unlock**
2. The wallet auto-locks after 3 hours of inactivity (configurable in Settings)

---

## Development Workflow

When iterating on the frontend, rebuild and restart:

```bash
# Terminal 1: rebuild frontend after changes
cd desktop/app
npm run build

# Terminal 2: restart backend (serves the new build)
cd desktop/backend
dotnet run
```

> **Tip:** For faster iteration, you can run `npx vite build --watch` in `desktop/app` to auto-rebuild on file changes, then just refresh the browser.

---

## Production Publish

### Single Target (PowerShell)

```powershell
.\publish.ps1                          # Default: win-x64
.\publish.ps1 -Runtime linux-x64      # Single RID
.\publish.ps1 -Runtime osx-arm64      # Apple Silicon Mac
```

### Single Target (Bash — Linux / macOS / CI)

```bash
chmod +x publish.sh
./publish.sh                           # Auto-detects host platform
./publish.sh linux-x64                 # Explicit RID
./publish.sh osx-arm64
```

### Multi-Platform Desktop

```powershell
# PowerShell
.\publish.ps1 -All                     # All 6 desktop targets
.\publish.ps1 -Windows                 # win-x64 + win-arm64
.\publish.ps1 -Linux                   # linux-x64 + linux-arm64
.\publish.ps1 -Mac                     # osx-x64 + osx-arm64
.\publish.ps1 -Windows -Linux          # Combine flags
.\publish.ps1 -All -SkipFrontend       # Skip npm build (backend-only iteration)
```

```bash
# Bash
./publish.sh --all                     # All 6 desktop targets
./publish.sh --windows                 # win-x64 + win-arm64
./publish.sh --linux                   # linux-x64 + linux-arm64
./publish.sh --mac                     # osx-x64 + osx-arm64
./publish.sh --skip-frontend linux-x64
```

### Mobile Builds (Capacitor)

Mobile builds wrap the React frontend in a native WebView shell via Capacitor.

```powershell
# PowerShell
.\publish.ps1 -Mobile                  # Android (+ iOS on macOS)
.\publish.ps1 -All -Mobile             # Desktop + mobile
```

```bash
# Bash
./publish.sh --mobile
./publish.sh --all --mobile
```

**First-time mobile setup** (run once):

```bash
cd desktop/app
npm install @capacitor/core @capacitor/cli @capacitor/android @capacitor/ios --save
npx cap add android    # Generates android/ project
npx cap add ios        # Generates ios/ project (macOS only)
```

Prerequisites:
- **Android**: Java 17 + Android SDK (Android Studio or `sdkmanager`)
- **iOS**: macOS + Xcode 15+

### Supported Runtime Identifiers

| Platform | RID | Notes |
|----------|-----|-------|
| Windows x64 | `win-x64` | Default target |
| Windows ARM | `win-arm64` | Surface Pro X, etc. |
| Linux x64 | `linux-x64` | Ubuntu, Fedora, Debian, etc. |
| Linux ARM | `linux-arm64` | Raspberry Pi 4+, ARM servers |
| macOS Intel | `osx-x64` | Pre-2020 Macs |
| macOS Apple Silicon | `osx-arm64` | M1/M2/M3/M4 Macs |
| Android | — | APK via Capacitor |
| iOS | — | Xcode archive via Capacitor |

### Output

Desktop publish produces one folder per RID:

```
publish/
  win-x64/NeoWallet.Backend.exe
  linux-x64/NeoWallet.Backend
  osx-arm64/NeoWallet.Backend
  android/NeoWallet.apk
  ios/NeoWallet.xcarchive
```

To run a published desktop app:

```bash
./publish/win-x64/NeoWallet.Backend.exe          # Native window
./publish/linux-x64/NeoWallet.Backend --browser   # Opens in browser
```

### Git Hooks (Auto Version Bump)

The `hooks/` directory contains git hooks that automatically bump the patch version on every commit and tag it for release.

**One-time setup:**

```powershell
# PowerShell
.\hooks\install.ps1
```

```bash
# Bash
chmod +x hooks/*
./hooks/install.sh
```

This runs `git config core.hooksPath hooks`. After that:

- **Pre-commit**: Increments the patch number in `VERSION` (e.g. `1.0.4` → `1.0.5`) and stages it
- **Post-commit**: Creates a `v{version}` tag (e.g. `v1.0.5`) pointing at the new commit

To skip the auto-bump (merge commits, etc.): `git commit --no-verify`

### CI/CD (GitHub Actions)

The repository has three workflows:

#### 1. Libraries CI (`.github/workflows/libraries-ci.yml`)

Runs automatically on every push to `main` and on pull requests:
- Restores, builds, and tests all .NET projects
- Gate-keeps code quality before merging

#### 2. Publish (`.github/workflows/publish.yml`)

Triggers on version tags (`v*`) or manual dispatch:
- Builds the React frontend once, shares it across all jobs
- Publishes desktop builds in parallel (Windows, Linux, macOS matrix — 6 RIDs)
- Builds Android APK and iOS archive via Capacitor
- Creates a GitHub Release with all artifacts on tag push

Manual dispatch lets you choose platforms:

```
platforms: "all"                    # Everything
platforms: "windows,linux"          # Desktop subset
platforms: "android,ios"            # Mobile only
platforms: "windows,android"        # Mix
```

#### 3. Version Bump (`.github/workflows/version-bump.yml`)

Manual dispatch for **major/minor** bumps (patch is automatic via git hook):
- **minor** — `1.0.5` → `1.1.0`
- **major** — `1.0.5` → `2.0.0`
- Optional pre-release label: `2.0.0-beta`
- Commits, tags, and triggers the Publish workflow via API

#### Typical release flow

```
1. Make changes, commit        → hook bumps VERSION (patch), creates tag
2. git push origin main --tags → CI tests + Publish workflow triggers
3. Publish builds all platforms, creates GitHub Release
```

For major/minor releases, use the Version Bump workflow from the GitHub Actions tab instead.

---

## Full Solution Build

Builds all .NET projects (SUS.EOS.Sharp, EosioSigningRequest, Backend, Tests):

```bash
cd SUS.EOS.NeoWallet
dotnet build SUS.EOS.NeoWallet.slnx
```

## Running Tests

```bash
cd SUS.EOS.NeoWallet
dotnet test SUS.EOS.NeoWallet.slnx
```

---

## Architecture Overview

```
Browser / Native Window (http://localhost:5199)
    │
    │  index.html, JS, CSS  ← static files served by .NET
    │  /api/*                ← JSON API
    │
.NET Backend (single process)
    ↓
SUS.EOS.EosioSigningRequest  ← ESR protocol
    ↓
SUS.EOS.Sharp                ← Antelope blockchain client
```

### Authentication Flow

1. Backend generates a random bearer token at startup
2. `/api/health`, `/api/wallet/create`, and `/api/wallet/unlock` are **open** (no token required)
3. Static files (HTML, JS, CSS) are also served without a token
4. On successful create/unlock, the response includes the bearer `token`
5. Frontend stores the token in `sessionStorage` and includes it in all subsequent API requests
6. Locking the wallet clears the token

### Wallet Storage

- Encrypted file at `%LocalAppData%/NeoWallet/wallet.json`
- AES-256-CBC encryption with PBKDF2 key derivation (100k iterations, SHA-256)
- Private keys are only available in memory while the wallet is unlocked

---

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/health` | No | Backend status, wallet state |
| POST | `/api/wallet/create` | No | Create new wallet (returns token) |
| POST | `/api/wallet/unlock` | No | Unlock wallet (returns token) |
| POST | `/api/wallet/lock` | Yes | Lock wallet |
| GET | `/api/wallet/summary` | Yes | Active account/network info |
| GET | `/api/accounts` | Yes | List imported accounts |
| POST | `/api/accounts/import` | Yes | Import accounts by private key |
| POST | `/api/accounts/lookup` | Yes | Find accounts by private key across chains |
| POST | `/api/accounts/remove` | Yes | Remove an imported account |
| POST | `/api/accounts/active` | Yes | Set active account |
| GET | `/api/networks` | Yes | List available networks |
| POST | `/api/networks/active` | Yes | Set active network |
| GET | `/api/balances?account=X&chainId=Y` | Yes | Get token balances |
| POST | `/api/transfers` | Yes | Sign and broadcast a transfer |
| POST | `/api/esr/parse` | Yes | Parse an ESR URI |
| POST | `/api/esr/approve` | Yes | Approve and broadcast ESR |
| POST | `/api/esr/reject` | Yes | Reject an ESR |
| GET | `/api/settings/autolock` | Yes | Get auto-lock timeout |
| POST | `/api/settings/autolock` | Yes | Set auto-lock timeout |

---

## Pages

| Page | Route | Description |
|------|-------|-------------|
| Dashboard | `/` | Wallet overview, balances, quick actions |
| Send | `/send` | Transfer tokens to another account |
| Receive | `/receive` | Account details for receiving tokens |
| Import | `/import` | Enter key → search chains → select accounts → import |
| Settings | `/settings` | Auto-lock, networks, accounts, chain badges |
| ESR Approval | `/esr` | Parse and approve EOSIO Signing Requests |
| Unlock | `/unlock` | Create wallet or unlock existing one |

---

## Supported Networks

| Network | Symbol | Chain ID |
|---------|--------|----------|
| WAX Mainnet | WAX | `1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4` |
| EOS Mainnet | EOS | `aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906` |
| Telos Mainnet | TLOS | `4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11` |

---

## Features

### Native Window

The app runs in a native window powered by Photino.NET (WebView2 on Windows). Pass `--browser` to use a web browser instead.

### Auto-Lock

The wallet locks automatically after a period of inactivity. Default: 3 hours. Configure in Settings or via the API. Set to 0 to disable.

### Chain Differentiation

Accounts are tagged with their chain. Color-coded badges appear throughout the UI:
- **WAX** — orange
- **EOS** — blue
- **TLOS** — purple

### Import Flow

1. Enter a private key (WIF format)
2. The app derives the public key and searches WAX, EOS, and Telos for linked accounts
3. Results are grouped by chain with color badges
4. Select which accounts to import
5. All selected accounts are imported in one batch

---

## Configuration

### Backend Port

Default: `5199`. Override via configuration:

```bash
dotnet run --Port=5200
```

### Backend Token (Development)

To use a fixed token for development/testing:

```bash
dotnet run --Auth:Token=my-dev-token
```

### Wallet Directory

Default: `%LocalAppData%/NeoWallet/`. Override via configuration:

```bash
dotnet run --Wallet:Directory=C:\my-wallet
```

---

## Troubleshooting

### "No frontend build found" warning on startup
- Run `npm run build` in `desktop/app/` first, then restart the backend

### "Backend unavailable" on Dashboard
- Make sure the backend is running on port 5199
- Check that you've unlocked the wallet first (the app should redirect to `/unlock`)

### "401 Unauthorized" errors
- The bearer token may have expired (backend was restarted)
- Lock and re-unlock the wallet, or refresh the page and unlock again

### TypeScript errors in VS Code
- Run `npm install` in `desktop/app/`
- The `@apply` and `@theme` CSS warnings are Tailwind v4 directives — they're handled by the Vite plugin at build time

### Wallet file location
- Windows: `%LocalAppData%\NeoWallet\wallet.json`
- To reset: delete the wallet file and restart the backend
