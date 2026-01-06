using SUS.EOS.NeoWallet.Repositories.Interfaces;
using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Repositories;

public class NetworkRepository : INetworkRepository
{
    /// <summary>
    /// Get predefined networks (WAX, EOS, Telos, etc.)
    /// Will be fetched from a remote source during startup later
    /// </summary>
    public Dictionary<string, NetworkConfig> GetPredefinedNetworks()
    {
        return new Dictionary<string, NetworkConfig>
        {
            ["wax"] = new()
            {
                ChainId = "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
                Name = "WAX",
                HttpEndpoint = "https://api.wax.alohaeos.com",
                KeyPrefix = "EOS",
                Symbol = "WAX",
                Precision = 8,
                BlockExplorer = "https://waxblock.io",
                Enabled = true,
            },
            ["wax-testnet"] = new()
            {
                ChainId = "f16b1833c747c43682f4386fca9cbb327929334a762755ebec17f6f23c9b8a12",
                Name = "WAX Testnet",
                HttpEndpoint = "https://testnet.waxsweden.org",
                KeyPrefix = "EOS",
                Symbol = "WAX",
                Precision = 8,
                BlockExplorer = "https://local.bloks.io",
                Enabled = false,
            },
            ["eos"] = new()
            {
                ChainId = "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
                Name = "EOS",
                HttpEndpoint = "https://api.eosn.io",
                KeyPrefix = "EOS",
                Symbol = "EOS",
                Precision = 4,
                BlockExplorer = "https://bloks.io",
                Enabled = true,
            },
            ["jungle4"] = new()
            {
                ChainId = "73e4385a2708e6d7048834fbc1079f2fabb17b3c125b146af438971e90716c4d",
                Name = "Jungle 4 Testnet",
                HttpEndpoint = "https://jungle4.greymass.com",
                KeyPrefix = "EOS",
                Symbol = "EOS",
                Precision = 4,
                BlockExplorer = "https://jungle4.bloks.io",
                Enabled = false,
            },
            ["telos"] = new()
            {
                ChainId = "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11",
                Name = "Telos",
                HttpEndpoint = "https://mainnet.telos.net",
                KeyPrefix = "EOS",
                Symbol = "TLOS",
                Precision = 4,
                BlockExplorer = "https://explorer.telos.net",
                Enabled = false,
            },
            ["telos-testnet"] = new()
            {
                ChainId = "1eaa0824707c8c16bd25145493bf062aecddfeb56c736f6ba6397f3195f33c9f",
                Name = "Telos Testnet",
                HttpEndpoint = "https://testnet.telos.net",
                KeyPrefix = "EOS",
                Symbol = "TLOS",
                Precision = 4,
                BlockExplorer = "https://explorer-test.telos.net",
                Enabled = false,
            },
            ["proton"] = new()
            {
                ChainId = "384da888112027f0321850a169f737c33e53b388aad48b5adace4bab97f437e0",
                Name = "Proton",
                HttpEndpoint = "https://proton.greymass.com",
                KeyPrefix = "EOS",
                Symbol = "XPR",
                Precision = 4,
                BlockExplorer = "https://www.protonscan.io",
                Enabled = false,
            },
            ["proton-testnet"] = new()
            {
                ChainId = "71ee83bcf52142d61019d95f9cc5427ba6a0d7ff8accd9e2088ae2abeaf3d3dd",
                Name = "Proton Testnet",
                HttpEndpoint = "https://testnet.protonchain.com",
                KeyPrefix = "EOS",
                Symbol = "XPR",
                Precision = 4,
                BlockExplorer = "https://proton-test.bloks.io",
                Enabled = false,
            },
            ["ux"] = new()
            {
                ChainId = "8fc6dce7942189f842170de953932b1f66693ad3788f766e777b6f9d22335c02",
                Name = "UX Network",
                HttpEndpoint = "https://api.uxnetwork.io",
                KeyPrefix = "EOS",
                Symbol = "UTX",
                Precision = 4,
                BlockExplorer = "https://explorer.uxnetwork.io",
                Enabled = false,
            },
            ["fio"] = new()
            {
                ChainId = "21dcae42c0182200e93f954a074011f9048a7624c6fe81d3c9541a614a88bd1c",
                Name = "FIO",
                HttpEndpoint = "https://fio.greymass.com",
                KeyPrefix = "FIO",
                Symbol = "FIO",
                Precision = 9,
                BlockExplorer = "https://fio.bloks.io",
                Enabled = false,
            },
            ["libre"] = new()
            {
                ChainId = "38b1d7815474d0c60683ecbea321d723e83f5da6ae5f1c1f9fecc69d9ba96465",
                Name = "Libre",
                HttpEndpoint = "https://libre.eosusa.io",
                KeyPrefix = "EOS",
                Symbol = "LIBRE",
                Precision = 4,
                BlockExplorer = "https://libre.bloks.io",
                Enabled = false,
            },
        };
    }
}
