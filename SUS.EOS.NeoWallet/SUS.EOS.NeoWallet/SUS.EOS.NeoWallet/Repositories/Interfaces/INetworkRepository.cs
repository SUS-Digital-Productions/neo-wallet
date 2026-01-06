using SUS.EOS.NeoWallet.Services.Models;

namespace SUS.EOS.NeoWallet.Repositories.Interfaces;

public interface INetworkRepository
{
    public Dictionary<string, NetworkConfig> GetPredefinedNetworks();
}
