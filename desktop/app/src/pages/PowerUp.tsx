import { useState } from "react";
import { Zap } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";

/**
 * eosio::powerup — temporarily rents CPU + NET resources via the PowerUp model.
 */
export default function PowerUp() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;
  const symbol = summary.data?.activeNetwork?.symbol ?? "EOS";

  const [receiver, setReceiver] = useState("");
  const [days, setDays] = useState("1");
  const [netFrac, setNetFrac] = useState("0");
  const [cpuFrac, setCpuFrac] = useState("0");
  const [maxPay, setMaxPay] = useState("");

  return (
    <ActionFormShell
      title="PowerUp"
      description="Temporarily rent CPU and NET via the PowerUp system"
      icon={<Zap className="size-5 text-primary" />}
      submitLabel="PowerUp"
      buildActions={() => {
        const a = requireActiveAccount(active);
        const d = parseInt(days, 10);
        const cpu = parseInt(cpuFrac, 10);
        const net = parseInt(netFrac, 10);
        if (!Number.isFinite(d) || d < 1) throw new Error("Days must be >= 1");
        if (!maxPay.trim()) throw new Error("Max payment is required");
        const pay = /\s/.test(maxPay.trim())
          ? maxPay.trim()
          : `${Number(maxPay).toFixed(4)} ${symbol}`;
        return [
          {
            account: "eosio",
            name: "powerup",
            data: {
              payer: a.account,
              receiver: receiver.trim() || a.account,
              days: d,
              net_frac: net,
              cpu_frac: cpu,
              max_payment: pay,
            },
          },
        ];
      }}
    >
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">PowerUp parameters</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1">
            <Label>Receiver (default: your account)</Label>
            <Input value={receiver} onChange={(e) => setReceiver(e.target.value)} />
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1">
              <Label>Days</Label>
              <Input
                inputMode="numeric"
                value={days}
                onChange={(e) => setDays(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>CPU fraction (0–10^15)</Label>
              <Input
                inputMode="numeric"
                value={cpuFrac}
                onChange={(e) => setCpuFrac(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>NET fraction (0–10^15)</Label>
              <Input
                inputMode="numeric"
                value={netFrac}
                onChange={(e) => setNetFrac(e.target.value)}
              />
            </div>
          </div>
          <div className="space-y-1">
            <Label>Max payment ({symbol})</Label>
            <Input
              inputMode="decimal"
              placeholder="1.0000"
              value={maxPay}
              onChange={(e) => setMaxPay(e.target.value)}
            />
          </div>
          <p className="text-xs text-muted-foreground">
            CPU/NET fractions are integers between 0 and 10^15 (waxblock-style).
            Use a chain explorer to estimate suitable values.
          </p>
        </CardContent>
      </Card>
    </ActionFormShell>
  );
}
