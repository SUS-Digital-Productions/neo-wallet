# ESR Listener Debugging Guide

## Changes Made

### 1. Added WAX Identity ESR Test
Created a new test in `EsrParsingTests.cs` to validate the WAX identity ESR you provided:
```csharp
[Fact]
public async Task ParseWaxIdentityEsr_ShouldSucceed()
```

This test confirms that:
- The ESR parses correctly
- Chain ID is correctly identified as WAX mainnet (`1064487b...`)
- Identity requests (type 3) are properly handled
- No action or transaction data is present (as expected for identity requests)

**Test Result: ✅ PASSING** - Your WAX ESR parses correctly!

### 2. Enhanced Debug Logging in EsrSessionManager

Added extensive debug logging throughout the ESR session manager to track:

**Connection Process:**
- WebSocket connection initiation
- Connection success/failure with state
- Connection errors with full exception details

**Message Reception:**
- When waiting for messages
- Message type, size, and content (first 200 chars)
- WebSocket state changes
- Close messages with status

**Message Processing:**
- Complete message content
- Envelope deserialization success/failure
- Message type identification
- Routing to appropriate handlers

**Request Handling:**
- ESR payload parsing (first 50 chars)
- Chain ID and callback extraction
- Session matching results
- Event subscriber presence
- Event raising confirmation

**Error Tracking:**
- Full exception messages
- Stack traces
- Error locations

### 3. Enhanced Debug Logging in MainPage

Already had logging for:
- ESR session manager startup
- Connection status changes
- Signing request reception
- Event handling errors

## Debugging Steps

### Step 1: Enable Debug Output Window
1. In Visual Studio, go to **View** → **Output**
2. In the Output window dropdown, select **Debug**
3. Run your application

### Step 2: Monitor Connection
When MainPage appears, you should see:
```
[MAINPAGE] OnAppearing called
[MAINPAGE] Wallet unlocked: True/False
[MAINPAGE] Starting ESR session manager...
[ESR] Initiating connection to wss://cb.anchor.link/{linkId}
[ESR] Connecting to WebSocket...
[ESR] WebSocket connected! State: Open
[ESR] Starting message listener task...
[MAINPAGE] ESR session manager started. LinkId: {guid}
[ESR] Message listener started
[ESR] Waiting for WebSocket message...
```

### Step 3: Test ESR Request
When you send an ESR request (e.g., from a dApp), you should see:
```
[ESR] Received message: Type=Text, Count=XXX, EndOfMessage=True
[ESR] Complete message received (XXX chars): {"type":"identity","payload":"esr:..."}...
[ESR] Processing message: {"type":"identity",...}
[ESR] Message type: identity
[ESR] Handling identity request...
[ESR] Parsing identity ESR: esr:g2PgYmZgYLjCyJNpw8BknVFSUlBspa-fnKSXmJ...
[ESR] Parsed identity request: ChainId=1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4
[ESR] Raising SigningRequestReceived event. Has subscribers: True
[ESR] Identity event raised successfully
[MAINPAGE] ESR Signing request received from: Unknown
```

## Common Issues and Solutions

### Issue 1: No Connection
**Symptoms:**
```
[ESR] Connection failed: [error message]
```

**Possible Causes:**
- Network firewall blocking WebSocket connections
- No internet connectivity
- cb.anchor.link service is down

**Solution:**
- Check internet connection
- Test WebSocket connectivity: `Test-NetConnection -ComputerName cb.anchor.link -Port 443`
- Check firewall settings

### Issue 2: Connection Succeeds but No Messages
**Symptoms:**
```
[ESR] WebSocket connected! State: Open
[ESR] Message listener started
[ESR] Waiting for WebSocket message...
[ESR] Waiting for WebSocket message...
[ESR] Waiting for WebSocket message...
```
(No messages received)

**Possible Causes:**
- LinkId not communicated to the dApp
- dApp not sending to correct LinkId
- WebSocket connection dropped silently

**Solution:**
1. Check the LinkId: Look for `[MAINPAGE] ESR session manager started. LinkId: {guid}`
2. Verify the dApp is using this exact LinkId
3. Test connection state: Add a button to check `_esrSessionManager.Status`

### Issue 3: Messages Received but Event Not Fired
**Symptoms:**
```
[ESR] Raising SigningRequestReceived event. Has subscribers: False
```

**Possible Causes:**
- Event subscription not set up
- MainPage disposed before event fires
- UI thread issues

**Solution:**
- Verify event subscription in MainPage constructor:
  ```csharp
  _esrSessionManager.SigningRequestReceived += OnEsrSigningRequestReceived;
  ```
- Check that MainPage is still active

### Issue 4: ESR Parsing Fails
**Symptoms:**
```
[ESR] Failed to handle identity request: [parse error]
[ESR] Stack trace: ...
```

**Possible Causes:**
- Invalid ESR format
- Unsupported ESR version
- Corrupted payload

**Solution:**
- Check the ESR format matches v3 spec
- Verify base64url encoding is correct
- Test the ESR in the unit test first

## Testing Your Specific WAX ESR

Your ESR:
```
esr:g2PgYmZgYLjCyJNpw8BknVFSUlBspa-fnKSXmJeckV-kl5OZl62fZmFsYJhknqxrkWRqomtiaWEAZCVa6pqZJhoYpxokmydZmjGxgJReZESY9lXj3KzTOSJlN34f3uFf3mfrwNS5pGrebk6vArV1xmGxIksZHcF2-ICsMNYz0zNQcCrKLy9OLQopSswrLsgvKgELGyr45ldl5uQk6psClWiEZ-alAFUp-IUoGBroGVgrAAXMTKwVKsxMNBUcCwpyUsNTk7wzS_RNjc31jM0UNLw9Qnx9dBRyMrNTFdxTk7PzNRWcM4ryc1P1DU0M9QxAUCE4MS2xKBOmxT8gSN_QyBQix1qcnF-QypGUk59drJeZDwA
```

This ESR:
- ✅ **Parses successfully** (confirmed by test)
- ✅ **Chain**: WAX mainnet (1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4)
- ✅ **Type**: Identity request (type 3)
- ✅ **Version**: ESR v3
- ✅ **Expected behavior**: Should trigger `OnEsrSigningRequestReceived` event with `IsIdentityRequest = true`

## Manual Testing Procedure

### Test 1: Direct ESR Parse
Add a button in your UI:
```csharp
private async void OnTestEsrClicked(object sender, EventArgs e)
{
    var esr = "esr:g2PgYmZgYLjCyJNpw8BknVFSUlBspa...";  // Your full ESR
    var service = new EsrService();
    try
    {
        var request = await service.ParseRequestAsync(esr);
        await DisplayAlert("Success", $"Parsed ESR for chain: {request.ChainId}", "OK");
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", ex.Message, "OK");
    }
}
```

### Test 2: Simulate WebSocket Message
Add a test button to simulate receiving an ESR message:
```csharp
private async void OnSimulateEsrClicked(object sender, EventArgs e)
{
    var args = new EsrSigningRequestEventArgs
    {
        Request = await new EsrService().ParseRequestAsync("esr:g2PgYmZgYLjCyJNpw8..."),
        RawPayload = "esr:g2PgYmZgYLjCyJNpw8...",
        IsIdentityRequest = true,
        Callback = "https://example.com/callback"
    };
    
    OnEsrSigningRequestReceived(_esrSessionManager, args);
}
```

### Test 3: Check WebSocket State
Add a status display:
```csharp
private void OnCheckStatusClicked(object sender, EventArgs e)
{
    var status = _esrSessionManager.Status;
    var linkId = _esrSessionManager.LinkId;
    DisplayAlert("ESR Status", $"Status: {status}\nLinkId: {linkId}", "OK");
}
```

## Next Steps

1. **Run the application** with debug output enabled
2. **Copy all debug logs** from the Output window when trying to use ESR
3. **Look for specific error patterns** matching the "Common Issues" section above
4. **Test with manual buttons** to isolate whether the issue is:
   - ESR parsing (Test 1)
   - Event handling (Test 2)
   - WebSocket connection (Test 3)

## Identity Request Signing

For identity requests on WAX:
- No transaction to sign (identity requests don't have actions)
- Response should include the account name and public key
- No blockchain broadcast needed

The signing flow should:
1. Parse the identity request
2. Show the popup asking user to approve linking their account
3. Return the account name and public key in the callback
4. No signature required (identity requests are not transactions)

## Files Modified

- `SUS.EOS.Sharp.Tests/EsrParsingTests.cs` - Added WAX ESR test
- `SUS.EOS.NeoWallet/Services/EsrSessionManager.cs` - Added extensive debug logging
- All existing debug logging in MainPage.xaml.cs retained

## Build Status

✅ **Build successful** - 0 errors, 5 warnings (all obsolete API warnings, non-critical)
✅ **Tests passing** - 2/2 tests pass (including new WAX ESR test)
