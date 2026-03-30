#!/usr/bin/env bash
#
# publish.sh — Cross-platform build script for Neo Wallet.
#
# Usage:
#   ./publish.sh                         # Auto-detect host platform
#   ./publish.sh linux-x64               # Single RID
#   ./publish.sh --all                   # All 6 desktop platforms
#   ./publish.sh --windows               # win-x64, win-arm64
#   ./publish.sh --linux                 # linux-x64, linux-arm64
#   ./publish.sh --mac                   # osx-x64, osx-arm64
#   ./publish.sh --mobile                # Android (+ iOS on macOS)
#   ./publish.sh --skip-frontend linux-x64
#

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKIP_FRONTEND=false
BUILD_MOBILE=false
TARGETS=()

# ── Parse arguments ──────────────────────────────────────

while [[ $# -gt 0 ]]; do
  case "$1" in
    --all|--desktop)
      TARGETS+=(win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)
      ;;
    --windows)
      TARGETS+=(win-x64 win-arm64)
      ;;
    --linux)
      TARGETS+=(linux-x64 linux-arm64)
      ;;
    --mac)
      TARGETS+=(osx-x64 osx-arm64)
      ;;
    --mobile)
      BUILD_MOBILE=true
      ;;
    --skip-frontend)
      SKIP_FRONTEND=true
      ;;
    -*)
      echo "Unknown option: $1" >&2
      exit 1
      ;;
    *)
      TARGETS+=("$1")
      ;;
  esac
  shift
done

# Default: auto-detect host platform
if [[ ${#TARGETS[@]} -eq 0 && "$BUILD_MOBILE" == "false" ]]; then
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64)   TARGETS=(linux-x64)   ;;
    Linux-aarch64)  TARGETS=(linux-arm64)  ;;
    Darwin-x86_64)  TARGETS=(osx-x64)     ;;
    Darwin-arm64)   TARGETS=(osx-arm64)    ;;
    MINGW*|MSYS*|CYGWIN*)
      TARGETS=(win-x64)
      ;;
    *)
      echo "Could not detect platform. Pass a RID explicitly." >&2
      exit 1
      ;;
  esac
fi

TOTAL=${#TARGETS[@]}
if [[ "$BUILD_MOBILE" == "true" ]]; then ((TOTAL++)) || true; fi

echo "=== Neo Wallet Publish ==="
echo "Targets: ${TARGETS[*]:-} ${BUILD_MOBILE:+mobile}"
echo ""

# ── Step 1: Build React frontend ────────────────────────

if [[ "$SKIP_FRONTEND" == "false" ]]; then
  echo "[frontend] Building React app..."
  pushd "$ROOT/desktop/app" > /dev/null
  npm install --silent
  npm run build
  popd > /dev/null
  echo "[frontend] Done"
  echo ""
fi

# ── Step 2: Publish desktop targets ─────────────────────

STEP=0
for RID in "${TARGETS[@]}"; do
  ((STEP++))
  echo "[$STEP/$TOTAL] Publishing $RID..."

  PUBLISH_DIR="$ROOT/publish/$RID"
  dotnet publish "$ROOT/desktop/backend/NeoWallet.Backend.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    --output "$PUBLISH_DIR"

  # Make executable on non-Windows
  if [[ "$RID" != win-* ]]; then
    chmod +x "$PUBLISH_DIR/NeoWallet.Backend" 2>/dev/null || true
  fi

  echo "[$STEP/$TOTAL] $RID -> $PUBLISH_DIR"
done

# ── Step 3: Mobile builds (Capacitor) ───────────────────

if [[ "$BUILD_MOBILE" == "true" ]]; then
  ((STEP++))
  echo "[$STEP/$TOTAL] Building mobile targets..."
  pushd "$ROOT/desktop/app" > /dev/null

  # Ensure Capacitor
  if [[ ! -d "node_modules/@capacitor/core" ]]; then
    echo "  Installing Capacitor..."
    npm install @capacitor/core @capacitor/cli --save
  fi

  npx cap sync 2>/dev/null || true

  # Android
  if [[ -d "android" ]]; then
    echo "  Building Android APK..."
    pushd android > /dev/null
    chmod +x gradlew
    ./gradlew assembleRelease
    APK=$(find . -name "*.apk" -path "*/release/*" | head -1)
    if [[ -n "$APK" ]]; then
      mkdir -p "$ROOT/publish/android"
      cp "$APK" "$ROOT/publish/android/NeoWallet.apk"
      echo "  Android APK -> publish/android/NeoWallet.apk"
    fi
    popd > /dev/null
  else
    echo "  Android project not initialized. Run: npx cap add android"
  fi

  # iOS (macOS only)
  if [[ -d "ios" ]]; then
    if command -v xcodebuild &>/dev/null; then
      echo "  Building iOS archive..."
      pushd ios/App > /dev/null
      xcodebuild archive \
        -workspace App.xcworkspace \
        -scheme App \
        -configuration Release \
        -archivePath "$ROOT/publish/ios/NeoWallet.xcarchive" \
        CODE_SIGNING_ALLOWED=NO \
        -destination "generic/platform=iOS" \
        | tail -5
      popd > /dev/null
      echo "  iOS archive -> publish/ios/NeoWallet.xcarchive"
    else
      echo "  iOS builds require macOS + Xcode. Skipped."
    fi
  else
    echo "  iOS project not initialized. Run: npx cap add ios (on macOS)"
  fi

  popd > /dev/null
fi

# ── Summary ──────────────────────────────────────────────

echo ""
echo "=== Publish Complete ==="
for RID in "${TARGETS[@]}"; do
  if [[ "$RID" == win-* ]]; then
    EXE="NeoWallet.Backend.exe"
  else
    EXE="NeoWallet.Backend"
  fi
  echo "  $RID -> publish/$RID/$EXE"
done

if [[ "$BUILD_MOBILE" == "true" && -f "$ROOT/publish/android/NeoWallet.apk" ]]; then
  echo "  android -> publish/android/NeoWallet.apk"
fi
if [[ "$BUILD_MOBILE" == "true" && -d "$ROOT/publish/ios/NeoWallet.xcarchive" ]]; then
  echo "  ios -> publish/ios/NeoWallet.xcarchive"
fi

echo ""
echo "Desktop apps open in a native window. Use --browser to open in a web browser instead."
