# neo-wallet

```
╔═══════════════════════════════════════════════════════════════════════╗
║                                                                       ║
║   ⚠️  WARNING: THIS CODE WAS FULLY AI-GENERATED (VIBED) ⚠️           ║
║                                                                       ║
║   This wallet implementation was created using AI assistance and     ║
║   has NOT undergone professional security audit or extensive real-   ║
║   world testing. USE AT YOUR OWN RISK!                               ║
║                                                                       ║
║   🔐 SECURITY NOTICE:                                                 ║
║   • Do NOT use with large amounts of cryptocurrency                  ║
║   • Always test on testnets first                                    ║
║   • Back up your private keys externally                             ║
║   • Review the code yourself before use                              ║
║   • No warranty or guarantees provided                               ║
║                                                                       ║
║   By using this software, you acknowledge and accept full            ║
║   responsibility for any potential loss of funds or data.            ║
║                                                                       ║
╚═══════════════════════════════════════════════════════════════════════╝
```

## SUS.EOS.NeoWallet

A cross-platform EOSIO/Antelope blockchain wallet built with .NET MAUI, inspired by the [Greymass Anchor wallet](https://github.com/greymass/anchor).

### Features

- 🔐 **Secure Storage**: AES-256-CBC encryption with PBKDF2 key derivation (4500 iterations)
- 🌐 **Multi-Chain Support**: Compatible with WAX, EOS, Telos, and any Antelope blockchain
- 📱 **Cross-Platform**: Windows, macOS, iOS, Android (via .NET MAUI)
- 🔑 **Multiple Import Methods**: Private key, BIP39 mnemonic, file import
- 💾 **Encrypted Backup**: Anchor-compatible wallet.json format
- 🔗 **ESR Support**: EOSIO Signing Request protocol for external apps
- 🎯 **Anchor Callbacks**: Compatible callback system for dApp integration
- 🔧 **Full EOSIO Library**: Complete transaction signing and blockchain operations

### Architecture

This project consists of two main components:

1. **SUS.EOS.NeoWallet** - Cross-platform wallet UI (MAUI)
2. **SUS.EOS.Sharp** - Reusable EOSIO/Antelope blockchain library

### Quick Start

```bash
# Clone repository
git clone https://github.com/yourusername/neo-wallet.git
cd neo-wallet

# Build and run
cd SUS.EOS.NeoWallet
dotnet build
dotnet run --project SUS.EOS.NeoWallet.WinUI  # or .Droid, .iOS, .Mac
```

### Documentation

- [Implementation Summary](IMPLEMENTATION_SUMMARY.md) - Complete technical documentation
- [Cryptography Implementation](SUS.EOS.NeoWallet/SUS.EOS.Sharp.Tests/CRYPTO_IMPLEMENTATION.md) - Cryptographic details

### License

MIT License - See [LICENSE](LICENSE) file for details

---

**Disclaimer**: This is experimental software. The developers are not responsible for any loss of funds or data.