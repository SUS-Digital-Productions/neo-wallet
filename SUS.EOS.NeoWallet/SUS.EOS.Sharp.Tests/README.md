# SUS.EOS.Sharp Test Project

Test console application for interacting with WAX blockchain using the SUS.EOS.Sharp library.

## Features

✅ **Fully Implemented:**
- ✅ Get chain information (block height, chain ID, etc.)
- ✅ Get account details (RAM, CPU, NET resources)
- ✅ Get token balances (WAX and other tokens)
- ✅ Build transaction objects
- ✅ EOSIO cryptographic signing (secp256k1)
- ✅ Private key handling (WIF format support)
- ✅ Public key derivation from private key
- ✅ Transaction serialization (ABI binary encoding)
- ✅ Signature encoding (EOSIO SIG_K1_ format)
- ✅ Transaction broadcasting to blockchain
- ✅ Full end-to-end transaction flow

## Running Tests

```bash
cd SUS.EOS.Sharp.Tests
dotnet run
```

## Test Account

The tests use the account from your link:
- **Account**: `testingpoint`
- **Contract**: `testingpoint`
- **Action**: `addboost`
- **Explorer**: https://waxblock.io/account/testingpoint?action=addboost

## Cryptographic Implementation

### ✅ Included Libraries:

- **Cryptography.ECDSA.Secp256k1** v1.1.3 - secp256k1 elliptic curve cryptography
- **Portable.BouncyCastle** v1.9.0 - Key management and base58 encoding

### ✅ Implemented Components:

1. **EosioKey.cs** - Private key handling
   - WIF format parsing (5... legacy and PVT_K1_ modern)
   - Public key derivation using secp256k1
   - EOSIO public key encoding (EOS...)
   - Transaction signing with recoverable signatures

2. **EosioSerializer.cs** - ABI serialization
   - Binary transaction serialization
   - Varint encoding for variable-length integers
   - EOSIO name encoding (uint64)
   - Action and authorization serialization
   - Signing data preparation (chainId + tx + zeros)

3. **WaxSignatureProvider.cs** - Complete signing pipeline
   - Private key initialization
   - Transaction serialization
   - SHA256 hashing of signing data
   - secp256k1 signing with recovery
   - EOSIO signature encoding (SIG_K1_...)
   - RIPEMD160 checksum calculation

## Action Parameters

For the `addboost` action, you need to provide:
```csharp
data: new
{
    // Example parameters (check actual contract ABI):
    user = "testingpoint",
    quantity = "1.00000000 WAX",
    memo = "Boost transaction"
}
```

Check the actual parameters on WAX Block Explorer:
https://waxblock.io/account/testingpoint?action=addboost#contract-actions

## Security Notes

⚠️ **NEVER** hardcode private keys in source code!

Use environment variables or secure key storage:
```bash
# Set environment variable
$env:WAX_PRIVATE_KEY="your_private_key_here"

# Or use Windows Credential Manager
# Or use Azure Key Vault / AWS Secrets Manager
```

## WAX Blockchain Endpoints

- **Greymass**: https://wax.greymass.com (default)
- **EOS Nation**: https://wax.eosnation.io
- **BlockchainPoland**: https://wax.eu.eosamsterdam.net

## Resources

- WAX Documentation: https://developer.wax.io/
- EOSIO Developer Portal: https://developers.eos.io/
- WAX Block Explorer: https://waxblock.io/
- EOS-Sharp Reference: https://github.com/GetScatter/eos-sharp

## Output Example

```
═══════════════════════════════════════════════════════════
    SUS.EOS.Sharp - WAX Blockchain Test Application
═══════════════════════════════════════════════════════════

Configuration:
  Endpoint: https://wax.greymass.com
  Account: testingpoint
  Contract: testingpoint

📡 Test 1: Getting Chain Information...
─────────────────────────────────────────────────────────
✓ Chain ID: 1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4
✓ Server Version: 2.0.13
✓ Head Block: #123456789
✓ Last Irreversible Block: #123456500
✓ Head Block Time: 2026-01-04 16:30:00 UTC
✓ Producer: waxproducer1

... (more test output)
```
