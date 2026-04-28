import { useState } from "react";
import { Cpu, Wifi } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";

/**
 * Stake / Unstake CPU + NET via the system contract.
 * Uses `eosio::delegatebw` to stake and `eosio::undelegatebw` to unstake.
 * The receiver defaults to the active account but can be customized
 * (waxblock allows staking to other accounts).
 */
export default function Resources() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;
  const symbol = summary.data?.activeNetwork?.symbol ?? "EOS";

  const [mode, setMode] = useState<"stake" | "unstake">("stake");
  const [receiver, setReceiver] = useState("");
  const [cpu, setCpu] = useState("");
  const [net, setNet] = useState("");

  const recv = receiver.trim() || active?.account || "";

  function quantity(amount: string) {
    if (!amount.trim()) return `0.0000 ${symbol}`;
    if (/\s/.test(amount)) return amount.trim();
    return `${Number(amount).toFixed(4)} ${symbol}`;
  }

  return (
    <Tabs value={mode} onValueChange={(v) => setMode(v as "stake" | "unstake")}>
      <TabsList className="mb-4">
        <TabsTrigger value="stake">Stake</TabsTrigger>
        <TabsTrigger value="unstake">Unstake</TabsTrigger>
      </TabsList>

      <TabsContent value="stake">
        <ActionFormShell
          title="Stake CPU / NET"
          description="Delegate tokens to acquire CPU and NET bandwidth"
          icon={<Cpu className="size-5 text-primary" />}
          submitLabel="Stake"
          buildActions={() => {
            const a = requireActiveAccount(active);
            const cpuQ = quantity(cpu);
            const netQ = quantity(net);
            if (cpu === "" && net === "") throw new Error("Enter CPU and/or NET amount");
            return [
              {
                account: "eosio",
                name: "delegatebw",
                data: {
                  from: a.account,
                  receiver: recv || a.account,
                  stake_net_quantity: netQ,
                  stake_cpu_quantity: cpuQ,
                  transfer: false,
                },
              },
            ];
          }}
        >
          <FormBody
            symbol={symbol}
            receiver={receiver}
            setReceiver={setReceiver}
            cpu={cpu}
            setCpu={setCpu}
            net={net}
            setNet={setNet}
            mode="stake"
          />
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="unstake">
        <ActionFormShell
          title="Unstake CPU / NET"
          description="Reclaim staked tokens. Funds become liquid after the chain's refund period."
          icon={<Wifi className="size-5 text-primary" />}
          submitLabel="Unstake"
          buildActions={() => {
            const a = requireActiveAccount(active);
            const cpuQ = quantity(cpu);
            const netQ = quantity(net);
            if (cpu === "" && net === "") throw new Error("Enter CPU and/or NET amount");
            return [
              {
                account: "eosio",
                name: "undelegatebw",
                data: {
                  from: a.account,
                  receiver: recv || a.account,
                  unstake_net_quantity: netQ,
                  unstake_cpu_quantity: cpuQ,
                },
              },
            ];
          }}
        >
          <FormBody
            symbol={symbol}
            receiver={receiver}
            setReceiver={setReceiver}
            cpu={cpu}
            setCpu={setCpu}
            net={net}
            setNet={setNet}
            mode="unstake"
          />
        </ActionFormShell>
      </TabsContent>
    </Tabs>
  );
}

interface BodyProps {
  symbol: string;
  receiver: string;
  setReceiver: (v: string) => void;
  cpu: string;
  setCpu: (v: string) => void;
  net: string;
  setNet: (v: string) => void;
  mode: "stake" | "unstake";
}
function FormBody(p: BodyProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">
          {p.mode === "stake" ? "Stake to" : "Unstake from"}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="space-y-1">
          <Label>Receiver (defaults to your account)</Label>
          <Input
            placeholder="account name"
            value={p.receiver}
            onChange={(e) => p.setReceiver(e.target.value)}
          />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1">
            <Label>CPU amount ({p.symbol})</Label>
            <Input
              placeholder="0.0000"
              value={p.cpu}
              onChange={(e) => p.setCpu(e.target.value)}
              inputMode="decimal"
            />
          </div>
          <div className="space-y-1">
            <Label>NET amount ({p.symbol})</Label>
            <Input
              placeholder="0.0000"
              value={p.net}
              onChange={(e) => p.setNet(e.target.value)}
              inputMode="decimal"
            />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
