# Building Neo Wallet for Android

The Android build of Neo Wallet uses **Tauri Mobile** with an **embedded Rust HTTP backend**
(not the .NET sidecar). The Rust backend lives in `desktop/app/src-tauri/src/mobile_backend/`
and exposes the same REST API (`http://127.0.0.1:5199`) the React frontend already speaks.

## What works on Android

The mobile backend now implements every endpoint the desktop frontend depends on except
the WebSocket-driven Anchor Link relay (which is desktop-only):

| Feature                              | Mobile  | Notes                                          |
| ------------------------------------ | ------- | ---------------------------------------------- |
| Wallet create / unlock / lock        | yes     | byte-compatible with desktop wallet.json       |
| Wallet export / import               | yes     | NeoWallet AES-CBC format                       |
| Wallet import-anchor                 | yes     | PBKDF2 + scrypt, multi-envelope detection      |
| Accounts list / import / remove      | yes     |                                                |
| Keys list / add / remove             | yes     |                                                |
| Networks list / set-active           | yes     | WAX / EOS / Telos hardcoded                    |
| Balances + currency-balance          | yes     | passthrough to nodeos                          |
| Transfer (eosio.token)               | yes     | embedded ABI for fast path                     |
| Generic action signing               | yes     | `/api/actions/sign` — fetches ABIs dynamically |
| Chain account / table-rows           | yes     | passthrough to nodeos                          |
| Settings (auto-lock, app)            | partial | persistence not yet wired                      |
| ESR parse / approve / sign-raw       | **no**  | returns 501 — mobile devices use deep-links    |
| ESR Anchor Link WebSocket relay      | stub    | accepts connection so frontend doesn't loop    |
| Deep-link handling (`esr://`)        | yes     | uses `tauri-plugin-deep-link`                  |

ESR signing requests reach the mobile app via OS deep-links (a dApp opens `esr://...`
which Android dispatches to Neo Wallet). The full Anchor Link relay (over WebSocket
to `cb.anchor.link`) is desktop-only because mobile platforms heavily restrict
long-running background connections.

## One-time setup

1. **Install Android Studio** and via *SDK Manager*:
   - **Android SDK** (API 33+).
   - **Android NDK** (26.x or newer — required by `ring` and other crypto crates).
   - **CMake**.

2. **Set environment variables** (PowerShell, persistent):

   ```powershell
   [Environment]::SetEnvironmentVariable("ANDROID_HOME",  "$env:LOCALAPPDATA\Android\Sdk", "User")
   [Environment]::SetEnvironmentVariable("NDK_HOME",      "$env:LOCALAPPDATA\Android\Sdk\ndk\26.3.11579264", "User")
   [Environment]::SetEnvironmentVariable("ANDROID_NDK_HOME", "$env:LOCALAPPDATA\Android\Sdk\ndk\26.3.11579264", "User")
   ```

   Adjust the NDK version path to match what the SDK Manager actually installed.

3. **Install Java 17 JDK** and put it on PATH (Android Gradle 8 requires JDK 17).

4. **Install Rust Android targets:**

   ```powershell
   rustup target add aarch64-linux-android armv7-linux-androideabi i686-linux-android x86_64-linux-android
   ```

5. **Install the Tauri CLI:**

   ```powershell
   cargo install tauri-cli --version "^2"
   # or:  npm install -g @tauri-apps/cli@^2
   ```

## Initialize the Android project

`gen/android/` is generated, not committed. Run once:

```powershell
cd desktop\app
npx tauri android init
```

This creates `src-tauri/gen/android/` with a Gradle project that wraps the Rust crate
into an `.aar` and produces an APK.

## Run on a connected device / emulator

```powershell
cd desktop\app
npx tauri android dev
```

The dev server runs at `http://localhost:1420` (Vite) and is forwarded to the device
via `adb reverse`. The Rust backend starts inside the app on `127.0.0.1:5199`. You
will see `[mobile-backend] BACKEND_TOKEN=...` in `adb logcat`.

## Build a release APK / AAB

```powershell
cd desktop\app
npx tauri android build           # APK + AAB
npx tauri android build --apk     # APK only
```

Output: `desktop/app/src-tauri/gen/android/app/build/outputs/`.

## Common build errors

| Symptom                                                                | Fix                                                                       |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `failed to find tool "aarch64-linux-android-clang"`                    | NDK not installed or `NDK_HOME` not set. See setup step 2.                |
| `error: linking with cc failed` during `ring` build                    | Same — `ring` builds C asm; needs NDK clang on PATH.                      |
| `Could not resolve com.android.tools.build:gradle`                     | Open a terminal with internet and run `gradlew --refresh-dependencies`.   |
| `JAVA_HOME is not set` / `Unsupported class file major version`        | Install JDK 17 and set `JAVA_HOME`.                                       |
| App opens but blank screen                                             | Check `adb logcat -s Tauri:V`; the embedded backend log starts with `[mobile-backend]`. |

## Architectural notes

- The Tauri command `show_main_window` exists on all platforms and is a no-op on Android
  (Android doesn't have a `unminimize` concept). It's invoked from the renderer when an
  ESR signing request arrives.
- The frontend auto-detects mobile via `window.__TAURI_INTERNALS__` and routes API calls
  to `http://127.0.0.1:5199` instead of using a relative URL (see `desktop/app/src/api/client.ts`).
- Wallet files (`{app data dir}/wallet.json`) are byte-compatible across desktop and
  mobile. The desktop path is `%LocalAppData%/NeoWallet/wallet.json`; on Android it's
  whatever `Tauri.path.app_data_dir()` resolves to (typically the app-private data dir).
