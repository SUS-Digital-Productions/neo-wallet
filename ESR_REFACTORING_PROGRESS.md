# ESR Library Refactoring - Progress Report

## Goal
Refactor all ESR (EOSIO Signing Request) related classes from `SUS.EOS.Sharp` and `SUS.EOS.NeoWallet` into the new dedicated library: `SUS.EOS.EosioSigningRequest`

## Completed Work

### 1. Created Dedicated ESR Library Structure
**Project**: `SUS.EOS.EosioSigningRequest`
- ✅ Added project reference to `SUS.EOS.Sharp`
- ✅ Organized into `Models/` and `Services/` folders
- ✅ Each class in its own file (best practices)

### 2. Created Model Classes (`Models/` folder)
All models extracted and separated into individual files:

- ✅ `EsrRequestPayload.cs` - Payload wrapper for actions/transactions
- ✅ `EsrFlags.cs` - Enum for ESR request flags (Broadcast, Background)
- ✅ `EsrCallbackResponse.cs` - Response from signing operations
- ✅ `EsrSession.cs` - Linked dApp session data
- ✅ `EsrSessionStatus.cs` - Enum for connection status (Disconnected, Connecting, Connected)
- ✅ `EsrSigningRequestEventArgs.cs` - Event args for signing requests
- ✅ `EsrSessionStatusEventArgs.cs` - Event args for status changes
- ✅ `EsrCallbackPayload.cs` - Callback payload for WebSocket responses
- ✅ `EsrMessageEnvelope.cs` - Internal WebSocket message wrapper

**Namespace**: `SUS.EOS.EosioSigningRequest.Models`

### 3. Created Service Interfaces (`Services/` folder)

- ✅ `IEsrService.cs` - ESR parsing and signing service interface
  - `ParseRequestAsync()` - Parse ESR from URI
  - `SignRequestAsync()` - Sign with optional broadcasting
  - `SignAndBroadcastAsync()` - Sign and always broadcast
  - `SendCallbackAsync()` - Send response to callback URL

- ✅ `IEsrSessionManager.cs` - WebSocket session manager interface (Anchor Link compatible)
  - `ConnectAsync()` / `DisconnectAsync()` - WebSocket lifecycle
  - `SendCallbackAsync()` - Send WebSocket callbacks
  - `AddSessionAsync()` / `RemoveSessionAsync()` / `ClearSessionsAsync()` - Session management
  - Events: `SigningRequestReceived`, `StatusChanged`
  - Properties: `Status`, `LinkId`, `Sessions`

- ✅ `EsrService.cs` - Concrete implementation of `IEsrService`
  - Uses `EosioSigningRequest.FromUri()` for parsing
  - Integrates with `IAntelopeBlockchainClient` for chain info and broadcasting
  - Handles identity requests (type 3) vs transaction requests

**Namespace**: `SUS.EOS.EosioSigningRequest.Services`

### 4. Main ESR Class

- ⏳ `Eosio SigningRequest.cs` - Core ESR protocol implementation (**IN PROGRESS**)
  - Copied from `SUS.EOS.Sharp/ESR/EosioSigningRequest.cs`
  - Updated namespace to `SUS.EOS.EosioSigningRequest`
  - Removed duplicate class definitions (now in separate files)
  - **Status**: File created but may need cleanup

**Namespace**: `SUS.EOS.EosioSigningRequest`

## Remaining Work

### 5. Session Manager Implementation
Need to move from `SUS.EOS.NeoWallet`:
- ⏳ `EsrSessionManager.cs` - Full implementation with WebSocket handling
  - Location: `SUS.EOS.NeoWallet/Services/EsrSessionManager.cs`
  - Needs to be moved to: `SUS.EOS.EosioSigningRequest/Services/EsrSessionManager.cs`
  - Dependencies: `IPreferences` (from MAUI) - needs abstraction or dependency injection

### 6. Update References
- ⏳ Update `SUS.EOS.Sharp` - Remove old ESR folder and classes
- ⏳ Update `SUS.EOS.NeoWallet` - Add reference to new ESR library
- ⏳ Update `SUS.EOS.Sharp.Tests` - Update imports
- ⏳ Update all using statements across projects

### 7. Handle Dependencies
**Issue**: `EsrSessionManager` depends on `IPreferences` from Microsoft.Maui
**Solutions**:
1. Create an abstraction (`IPreferencesService`) in the ESR library
2. Accept `IPreferences` as a constructor parameter (keep MAUI dependency)
3. Move session manager to NeoWallet project (less ideal)

**Recommendation**: Option 1 - Create abstraction for maximum reusability

### 8. Test and Validate
- ⏳ Build all projects
- ⏳ Fix compilation errors
- ⏳ Run existing ESR tests
- ⏳ Update test project references

## File Organization

### New Library Structure:
```
SUS.EOS.EosioSigningRequest/
├── EosioSigningRequest.cs (main class)
├── Models/
│   ├── EsrRequestPayload.cs
│   ├── EsrFlags.cs
│   ├── EsrCallbackResponse.cs
│   ├── EsrSession.cs
│   ├── EsrSessionStatus.cs
│   ├── EsrSigningRequestEventArgs.cs
│   ├── EsrSessionStatusEventArgs.cs
│   ├── EsrCallbackPayload.cs
│   └── EsrMessageEnvelope.cs
└── Services/
    ├── IEsrService.cs
    ├── EsrService.cs
    ├── IEsrSessionManager.cs
    └── EsrSessionManager.cs (TODO)
```

### Files to Remove (after migration):
```
SUS.EOS.Sharp/
└── ESR/
    └── EosioSigningRequest.cs (DELETE after confirming new version works)

SUS.EOS.NeoWallet/
├── Services/
│   ├── EsrSessionManager.cs (MOVE to new library)
│   └── Interfaces/
│       └── IEsrSessionManager.cs (DELETE - now in new library)
└── Services/Models/EsrSession/
    ├── EsrSession.cs (DELETE)
    ├── EsrSessionStatus.cs (DELETE)
    ├── EsrSigningRequestEventArgs.cs (DELETE)
    ├── EsrSessionStatusEventArgs.cs (DELETE)
    ├── EsrCallbackPayload.cs (DELETE)
    └── EsrMessageEnvelope.cs (DELETE)
```

## Dependencies Map

### SUS.EOS.EosioSigningRequest depends on:
- `SUS.EOS.Sharp` - For cryptography, serialization, models (ChainInfo, etc.)
- No direct MAUI dependencies (if we abstract IPreferences)

### Projects that will depend on SUS.EOS.EosioSigningRequest:
- `SUS.EOS.NeoWallet` - Main wallet app
- `SUS.EOS.Sharp.Tests` - Testing ESR functionality

## Next Steps

1. **Fix `EosioSigningRequest.cs`** - Remove any remaining duplicate classes
2. **Create `IPreferencesService` abstraction** - For preferences storage
3. **Move `EsrSessionManager.cs`** - From NeoWallet to new library
4. **Update all project references**
5. **Build and fix compilation errors**
6. **Update test imports**
7. **Verify all functionality works**

## Benefits of This Refactoring

1. ✅ **Separation of Concerns** - ESR logic isolated from wallet and blockchain logic
2. ✅ **Reusability** - ESR library can be used in other projects
3. ✅ **Maintainability** - Each class in its own file
4. ✅ **Testability** - Easier to test ESR functionality in isolation
5. ✅ **Clear Dependencies** - ESR library has minimal external dependencies

## Current Status

**Progress**: ~70% complete
- ✅ Models separated
- ✅ Service interfaces created
- ✅ EsrService implementation done
- ⏳ EosioSigningRequest class migrated (needs validation)
- ⏳ Session manager migration pending
- ⏳ Reference updates pending
- ⏳ Build verification pending
