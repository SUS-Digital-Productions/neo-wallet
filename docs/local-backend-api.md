# Local Backend API Outline

## Purpose

This document defines the first-pass API shape for the future Tauri/React desktop shell talking to the local .NET backend.

The API is intentionally minimal. It should expose app capabilities, not internal library types.

## Transport

Recommended initial transport: localhost HTTP with a startup-generated bearer token.

Example:

```http
Authorization: Bearer <startup-token>
```

## Core Endpoints

### Health

`GET /api/health`

Returns backend version and readiness.

```json
{
  "status": "ok",
  "version": "0.1.0",
  "walletLoaded": true,
  "walletUnlocked": false
}
```

### Wallet Summary

`GET /api/wallet/summary`

```json
{
  "activeNetwork": {
    "chainId": "1064487b...",
    "name": "WAX Mainnet",
    "symbol": "WAX"
  },
  "activeAccount": {
    "account": "testingpoint",
    "authority": "active",
    "publicKey": "EOS6..."
  },
  "listenerStatus": "Connected"
}
```

### Accounts

`GET /api/accounts`

Returns available accounts for the active or requested network.

`POST /api/accounts/active`

```json
{
  "account": "testingpoint",
  "authority": "active",
  "chainId": "1064487b..."
}
```

### Networks

`GET /api/networks`

`POST /api/networks/active`

```json
{
  "chainId": "1064487b..."
}
```

### Balances

`GET /api/balances?account=testingpoint&chainId=1064487b...`

```json
[
  {
    "symbol": "WAX",
    "amount": "0.00189793 WAX",
    "numericAmount": 0.00189793
  }
]
```

### Unlock

`POST /api/wallet/unlock`

```json
{
  "password": "user-supplied-password"
}
```

Response:

```json
{
  "unlocked": true
}
```

### Send Transfer

`POST /api/transfers`

```json
{
  "chainId": "1064487b...",
  "from": "testingpoint",
  "authority": "active",
  "to": "receiveracct",
  "quantity": "1.00000000 WAX",
  "memo": "optional"
}
```

Response:

```json
{
  "transactionId": "abcd1234...",
  "broadcast": true
}
```

### ESR Intake

`POST /api/esr/parse`

```json
{
  "uri": "esr://..."
}
```

Response:

```json
{
  "requestId": "req_123",
  "chainId": "1064487b...",
  "type": "transaction",
  "actions": [
    {
      "account": "eosio.token",
      "name": "transfer"
    }
  ]
}
```

### ESR Approval

`POST /api/esr/approve`

```json
{
  "requestId": "req_123",
  "broadcast": true,
  "account": "myaccount",
  "authority": "active",
  "chainId": "1064487b..."
}
```

`account`, `authority`, and `chainId` are optional. When omitted, the backend uses the active wallet account.

### ESR Rejection

`POST /api/esr/reject`

```json
{
  "requestId": "req_123",
  "reason": "User rejected request"
}
```

## Events / Streaming

The frontend will need push updates for:
- incoming ESR requests
- listener status changes
- active account changes
- transaction completion

A simple first version can use polling. A better version can use:
- Server-Sent Events, or
- WebSocket, or
- Tauri event bridge if transport is later moved away from HTTP

## DTO Rules

- Do not return raw shared-library models directly to the renderer.
- Return stable, frontend-facing DTOs.
- Treat API payloads as versioned contracts.

## Non-Goals

- exposing decrypted private keys
- giving the renderer direct signing primitives
- forcing the renderer to understand low-level EOS/Antelope serialization models