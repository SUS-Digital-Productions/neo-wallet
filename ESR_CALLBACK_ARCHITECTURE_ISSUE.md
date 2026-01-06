# ESR Callback Architecture Issue - Root Cause Analysis

## TL;DR - The Problem

**The wallet is sending callbacks through the WRONG channel!**

Current implementation sends callbacks back through the **wallet's WebSocket connection**, but Anchor Link protocol requires callbacks to be sent to the **callback URL via HTTP POST**.

## Anchor Link Protocol Flow (Correct)

### 1. Session Establishment
```
dApp                           Forwarder                    Wallet
 |                                |                            |
 |---(QR/URI) Identity Request--->|                            |
 |                                |---(WebSocket Push)-------->|
 |                                |                            |
 |                                |<---(HTTP POST Callback)----|
 |<---(Long Poll/WebSocket)-------|                            |
 |                                                              |
Session created with:
- dApp request_key (for encryption)
- Wallet channel URL (for sending requests TO wallet)
- Wallet receive_key (for encrypting requests)
```

### 2. Transaction Signing (Where the bug is)
```
dApp                           Forwarder                    Wallet
 |                                |                            |
 | Generate callback_url          |                            |
 | (e.g., https://buoy.example.com/uuid)                       |
 |                                |                            |
 | Encrypt request with:          |                            |
 | - transaction                  |                            |
 | - callback: callback_url       |                            |
 |                                |                            |
 |---(HTTP POST to wallet_ch)---->|                            |
 |                                |---(WebSocket Push)-------->|
 |                                |                            |
 |                                |         User signs         |
 |                                |                            |
 |                                |                     ❌ WRONG: Send via WebSocket
 |                                |                     ✅ RIGHT: HTTP POST to callback_url
 |                                |                            |
 |                                |<---(HTTP POST)-------------|
 |<---(Long Poll/WebSocket)-------|   to callback_url          |
 |   response received            |                            |
```

## What NeoWallet Is Currently Doing (WRONG)

### In `EsrSessionManager.SendCallbackAsync`:
```csharp
public async Task SendCallbackAsync(EsrCallbackPayload callback)
{
    if (_webSocket?.State != WebSocketState.Open)
    {
        return;
    }

    // ❌ WRONG: Sending back through wallet's WebSocket
    var message = new { type = "callback", payload = callback };
    var json = JsonSerializer.Serialize(message);
    var bytes = Encoding.UTF8.GetBytes(json);
    await _webSocket.SendAsync(
        new ArraySegment<byte>(bytes),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None
    );
}
```

**Problem:** This sends the callback back through the **wallet's receiving WebSocket** (the one the wallet connects to for receiving requests). But the dApp is NOT listening on this WebSocket - the dApp is listening on the **callback URL** it specified in the request!

## What Should Happen (Anchor/WharfKit Pattern)

### Anchor TypeScript (Correct)
```typescript
// After signing
const callback = resolved.getCallback([signature], blockNum);
// callback = { url: "https://buoy.example.com/uuid", payload: {...} }

// Send via HTTP POST to callback URL
await fetch(callback.url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(callback.payload)
});
```

### What NeoWallet Should Do
```csharp
public async Task SendCallbackAsync(EsrCallbackPayload callback, string callbackUrl)
{
    if (string.IsNullOrEmpty(callbackUrl))
    {
        System.Diagnostics.Trace.WriteLine("[ESR] No callback URL specified");
        return;
    }

    try
    {
        // ✅ CORRECT: Send via HTTP POST to the callback URL
        using var httpClient = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(callback);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Sending callback to: {callbackUrl}");
        var response = await httpClient.PostAsync(callbackUrl, content);
        
        System.Diagnostics.Trace.WriteLine($"[ESR] Callback response: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"[ESR] Failed to send callback: {ex.Message}");
    }
}
```

## Why the Current Approach Doesn't Work

### The Two Different Channels

1. **Wallet Channel** (`link_ch` in callback payload)
   - **Direction**: dApp → Wallet
   - **Purpose**: dApp sends encrypted signing requests TO wallet
   - **URL Example**: `wss://cb.anchor.link/f6f4c8a1-1234-5678-abcd-0123456789ab`
   - **Who connects**: Wallet connects and listens
   - **Current implementation**: Uses this channel (WRONG for callbacks)

2. **Callback Channel** (`callback` in ESR request)
   - **Direction**: Wallet → dApp
   - **Purpose**: Wallet sends signed transaction response TO dApp
   - **URL Example**: `https://buoy.greymass.com/a3b2c1d4-9876-5432-fedc-ba0987654321`
   - **Who listens**: dApp long-polls or listens via WebSocket
   - **Should be used**: For sending callbacks (THIS is what we need!)

### Visual Representation

```
Wallet Channel (link_ch)              Callback Channel (callback)
=====================                 =========================
dApp generates requests   -------->   Wallet generates responses
Wallet listens here                   dApp listens here
WebSocket/HTTP POST                   HTTP POST / WebSocket

Example:                              Example:
wss://cb.anchor.link/                 https://buoy.greymass.com/
  f6f4c8a1-1234-5678-abcd/              a3b2c1d4-9876-5432-fedc/
```

## Evidence from Anchor Link Code

### From `anchor-link/protocol.md`:
```python
# Wallet receives request on its channel
def handle_channel_push(encrypted):
    request = decrypt(encrypted)
    signature = sign_request(request)
    response = esr_make_response(request, signature)
    
    # ✅ Send callback to the URL specified in the request
    send_callback(response, request.get_callback())  # <-- HTTP POST to callback URL!
```

### From `anchor-link/src/link-session.ts`:
```typescript
// LinkChannelSession.onRequest - Sends request TO wallet
fetch(this.channelUrl, {  // <-- Wallet's channel URL
    method: 'POST',
    body: encrypted_payload
})

// Later, wallet response comes back via callback URL
// NOT via the wallet channel WebSocket!
```

## The Fix Required

### 1. Store Callback URL with Request
When a signing request is received, store the callback URL:

```csharp
private async Task HandleSigningRequestAsync(EsrMessageEnvelope envelope)
{
    var request = await _esrService.ParseRequestAsync(envelope.Payload);
    
    // Store the callback URL from the request
    var callbackUrl = request.Callback; // ✅ This is where responses should go
    
    var args = new EsrSigningRequestEventArgs
    {
        Request = request,
        CallbackUrl = callbackUrl,  // Pass it to the UI
        // ...
    };
    
    SigningRequestReceived?.Invoke(this, args);
}
```

### 2. Send Callback via HTTP POST (Not WebSocket)
Update the signing popup to send callback via HTTP:

```csharp
// In EsrSigningPopupPage.xaml.cs
if (!string.IsNullOrEmpty(callbackUrl))
{
    var callback = new EsrCallbackPayload { /* ... */ };
    
    // ✅ Send via HTTP POST to callback URL
    using var httpClient = new HttpClient();
    var json = JsonSerializer.Serialize(callback);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    await httpClient.PostAsync(callbackUrl, content);
}
```

### 3. Remove WebSocket Callback Method (Or Rename It)
The current `IEsrSessionManager.SendCallbackAsync` is misleading because it sends via WebSocket when it should send via HTTP POST.

**Options:**
- **Option A**: Remove it entirely, use `IEsrService.SendCallbackAsync` (which already does HTTP POST correctly!)
- **Option B**: Rename to `SendCallbackViaHttpAsync` and change implementation to HTTP POST
- **Option C**: Keep for future WebSocket callback support but don't use it for Anchor Link

## Why the Confusion Happened

The Anchor Link protocol has **bi-directional** communication:
1. **dApp → Wallet**: Via wallet channel (WebSocket/HTTP POST to `link_ch`)
2. **Wallet → dApp**: Via callback URL (HTTP POST to `callback`)

We mistakenly thought callbacks should go back through the wallet channel WebSocket, but that's the WRONG direction. The wallet channel is for dApp→Wallet requests only.

## Summary of Changes Needed

1. ✅ **DO**: Use `IEsrService.SendCallbackAsync()` which already does HTTP POST correctly
2. ❌ **DON'T**: Use `IEsrSessionManager.SendCallbackAsync()` for Anchor Link callbacks
3. 🔄 **CONSIDER**: Rename/remove `IEsrSessionManager.SendCallbackAsync()` to avoid confusion

## Testing the Fix

After implementing HTTP POST callbacks:
1. Open dApp (e.g., wax.bloks.io)
2. Click "Login with Anchor"
3. NeoWallet receives request via wallet channel WebSocket ✅
4. User signs transaction
5. NeoWallet sends callback via HTTP POST to callback URL ✅
6. dApp receives callback and updates UI ✅

## Conclusion

**The current implementation already has the correct HTTP callback method** in `IEsrService.SendCallbackAsync()`! The problem is that `EsrSigningPopupPage` was updated to use `IEsrSessionManager.SendCallbackAsync()` which sends via WebSocket instead of HTTP.

**Simple Fix:** Revert to using `IEsrService.SendCallbackAsync()` which does HTTP POST correctly, and only use it when `_request.Callback` is not empty (which is the standard Anchor Link pattern).
