import { useState } from "react";
import { UserPlus, Gavel } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";
import type { SignAction } from "@/api/types";

/**
 * Create new on-chain account (newaccount + buyrambytes + delegatebw),
 * and Name Bids (eosio::bidname).
 */
export default function CreateAccount() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;
  const symbol = summary.data?.activeNetwork?.symbol ?? "EOS";

  const [mode, setMode] = useState<"create" | "bid">("create");

  // Create-account state
  const [newName, setNewName] = useState("");
  const [ownerKey, setOwnerKey] = useState("");
  const [activeKey, setActiveKey] = useState("");
  const [ramBytes, setRamBytes] = useState("4096");
  const [stakeNet, setStakeNet] = useState("0.5000");
  const [stakeCpu, setStakeCpu] = useState("0.5000");
  const [transfer, setTransfer] = useState(false);

  // Bidname state
  const [bidName, setBidName] = useState("");
  const [bidAmount, setBidAmount] = useState("");

  function authority(key: string) {
    return {
      threshold: 1,
      keys: [{ key: key.trim(), weight: 1 }],
      accounts: [] as unknown[],
      waits: [] as unknown[],
    };
  }

  function quantity(value: string) {
    if (!value.trim()) return `0.0000 ${symbol}`;
    if (/\s/.test(value)) return value.trim();
    return `${Number(value).toFixed(4)} ${symbol}`;
  }

  return (
    <Tabs value={mode} onValueChange={(v) => setMode(v as typeof mode)}>
      <TabsList className="mb-4">
        <TabsTrigger value="create">Create account</TabsTrigger>
        <TabsTrigger value="bid">Bid name</TabsTrigger>
      </TabsList>

      <TabsContent value="create">
        <ActionFormShell
          title="Create account"
          description="Create a new on-chain account (newaccount + RAM + stake)"
          icon={<UserPlus className="size-5 text-primary" />}
          submitLabel="Create"
          buildActions={(): SignAction[] => {
            const a = requireActiveAccount(active);
            if (!newName.trim()) throw new Error("Account name is required");
            if (!activeKey.trim()) throw new Error("Active key is required");
            const owner = ownerKey.trim() || activeKey.trim();
            const ram = parseInt(ramBytes, 10);
            if (!Number.isFinite(ram) || ram <= 0)
              throw new Error("RAM bytes must be > 0");

            return [
              {
                account: "eosio",
                name: "newaccount",
                data: {
                  creator: a.account,
                  name: newName.trim(),
                  owner: authority(owner),
                  active: authority(activeKey.trim()),
                },
              },
              {
                account: "eosio",
                name: "buyrambytes",
                data: {
                  payer: a.account,
                  receiver: newName.trim(),
                  bytes: ram,
                },
              },
              {
                account: "eosio",
                name: "delegatebw",
                data: {
                  from: a.account,
                  receiver: newName.trim(),
                  stake_net_quantity: quantity(stakeNet),
                  stake_cpu_quantity: quantity(stakeCpu),
                  transfer,
                },
              },
            ];
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">New account</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="space-y-1">
                <Label>Account name (12 chars, a-z, 1-5, .)</Label>
                <Input
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                  placeholder="newaccount1"
                />
              </div>
              <div className="space-y-1">
                <Label>Active public key</Label>
                <Input
                  className="font-mono text-xs"
                  value={activeKey}
                  onChange={(e) => setActiveKey(e.target.value)}
                  placeholder="EOS… / PUB_K1_…"
                />
              </div>
              <div className="space-y-1">
                <Label>Owner public key (defaults to active)</Label>
                <Input
                  className="font-mono text-xs"
                  value={ownerKey}
                  onChange={(e) => setOwnerKey(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Initial resources</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="space-y-1">
                <Label>RAM bytes</Label>
                <Input
                  inputMode="numeric"
                  value={ramBytes}
                  onChange={(e) => setRamBytes(e.target.value)}
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Stake CPU ({symbol})</Label>
                  <Input
                    inputMode="decimal"
                    value={stakeCpu}
                    onChange={(e) => setStakeCpu(e.target.value)}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Stake NET ({symbol})</Label>
                  <Input
                    inputMode="decimal"
                    value={stakeNet}
                    onChange={(e) => setStakeNet(e.target.value)}
                  />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={transfer}
                  onChange={(e) => setTransfer(e.target.checked)}
                />
                Transfer stake ownership to new account
              </label>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="bid">
        <ActionFormShell
          title="Bid on a premium name"
          description="Place a bid for a short / premium account name (eosio::bidname)"
          icon={<Gavel className="size-5 text-primary" />}
          submitLabel="Place bid"
          buildActions={(): SignAction[] => {
            const a = requireActiveAccount(active);
            if (!bidName.trim()) throw new Error("Name is required");
            if (!bidAmount.trim()) throw new Error("Bid amount is required");
            return [
              {
                account: "eosio",
                name: "bidname",
                data: {
                  bidder: a.account,
                  newname: bidName.trim(),
                  bid: quantity(bidAmount),
                },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="space-y-1">
                <Label>Name to bid on</Label>
                <Input
                  value={bidName}
                  onChange={(e) => setBidName(e.target.value)}
                  placeholder="x"
                />
              </div>
              <div className="space-y-1">
                <Label>Bid ({symbol})</Label>
                <Input
                  inputMode="decimal"
                  value={bidAmount}
                  onChange={(e) => setBidAmount(e.target.value)}
                  placeholder="1.0000"
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Bids must be at least 10% higher than the previous bid for the same name.
              </p>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>
    </Tabs>
  );
}
