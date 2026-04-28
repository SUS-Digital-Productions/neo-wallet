import { useState } from "react";
import { HardDrive } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";

/**
 * Buy / Sell RAM via the system contract.
 * - Buy by token amount → eosio::buyram
 * - Buy by exact bytes  → eosio::buyrambytes
 * - Sell by bytes        → eosio::sellram
 */
export default function Ram() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;
  const symbol = summary.data?.activeNetwork?.symbol ?? "EOS";

  const [mode, setMode] = useState<"buy" | "buyBytes" | "sell">("buy");
  const [receiver, setReceiver] = useState("");
  const [amount, setAmount] = useState("");
  const [bytes, setBytes] = useState("");

  function quantity(input: string) {
    if (!input.trim()) return `0.0000 ${symbol}`;
    if (/\s/.test(input)) return input.trim();
    return `${Number(input).toFixed(4)} ${symbol}`;
  }

  return (
    <Tabs value={mode} onValueChange={(v) => setMode(v as typeof mode)}>
      <TabsList className="mb-4">
        <TabsTrigger value="buy">Buy ({symbol})</TabsTrigger>
        <TabsTrigger value="buyBytes">Buy (bytes)</TabsTrigger>
        <TabsTrigger value="sell">Sell</TabsTrigger>
      </TabsList>

      <TabsContent value="buy">
        <ActionFormShell
          title="Buy RAM"
          description={`Spend ${symbol} to acquire RAM at the current market price`}
          icon={<HardDrive className="size-5 text-primary" />}
          submitLabel="Buy RAM"
          buildActions={() => {
            const a = requireActiveAccount(active);
            if (!amount.trim()) throw new Error("Enter an amount");
            return [
              {
                account: "eosio",
                name: "buyram",
                data: {
                  payer: a.account,
                  receiver: receiver.trim() || a.account,
                  quant: quantity(amount),
                },
              },
            ];
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Spend {symbol}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="space-y-1">
                <Label>Receiver (default: your account)</Label>
                <Input
                  placeholder="account name"
                  value={receiver}
                  onChange={(e) => setReceiver(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label>Amount ({symbol})</Label>
                <Input
                  inputMode="decimal"
                  placeholder="1.0000"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="buyBytes">
        <ActionFormShell
          title="Buy RAM (bytes)"
          description="Buy a specific number of RAM bytes; cost is calculated automatically"
          icon={<HardDrive className="size-5 text-primary" />}
          submitLabel="Buy bytes"
          buildActions={() => {
            const a = requireActiveAccount(active);
            const n = parseInt(bytes, 10);
            if (!Number.isFinite(n) || n <= 0) throw new Error("Enter a positive byte count");
            return [
              {
                account: "eosio",
                name: "buyrambytes",
                data: {
                  payer: a.account,
                  receiver: receiver.trim() || a.account,
                  bytes: n,
                },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="space-y-1">
                <Label>Receiver (default: your account)</Label>
                <Input
                  placeholder="account name"
                  value={receiver}
                  onChange={(e) => setReceiver(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label>Bytes</Label>
                <Input
                  inputMode="numeric"
                  placeholder="4096"
                  value={bytes}
                  onChange={(e) => setBytes(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="sell">
        <ActionFormShell
          title="Sell RAM"
          description="Sell RAM bytes back to the system at the current market price"
          icon={<HardDrive className="size-5 text-primary" />}
          submitLabel="Sell RAM"
          buildActions={() => {
            const a = requireActiveAccount(active);
            const n = parseInt(bytes, 10);
            if (!Number.isFinite(n) || n <= 0) throw new Error("Enter a positive byte count");
            return [
              {
                account: "eosio",
                name: "sellram",
                data: { account: a.account, bytes: n },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="space-y-1">
                <Label>Bytes to sell</Label>
                <Input
                  inputMode="numeric"
                  placeholder="4096"
                  value={bytes}
                  onChange={(e) => setBytes(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>
    </Tabs>
  );
}
