import { useState } from "react";
import { Wrench, Send, Plus, Trash2, AlertCircle } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Separator } from "@/components/ui/separator";
import { useSignActions, useWalletSummary } from "@/api/hooks";

interface DraftAction {
  account: string;
  name: string;
  data: string; // JSON string for editing
}

const EMPTY_ACTION: DraftAction = {
  account: "eosio.token",
  name: "transfer",
  data: JSON.stringify(
    { from: "", to: "", quantity: "1.0000 WAX", memo: "" },
    null,
    2,
  ),
};

/**
 * Utilities / Generic action signer.
 * Lets the user assemble an arbitrary set of {contract, action, data} tuples
 * and sign + broadcast them as a single atomic transaction with the active
 * account. Useful for actions waxblock surfaces (claimrewards, custom contracts,
 * MSIG payloads, etc.) without needing a dedicated form.
 */
export default function Utilities() {
  const summary = useWalletSummary();
  const sign = useSignActions();
  const [actions, setActions] = useState<DraftAction[]>([{ ...EMPTY_ACTION }]);
  const [broadcast, setBroadcast] = useState(true);
  const [lastTx, setLastTx] = useState<string | null>(null);

  function update(i: number, patch: Partial<DraftAction>) {
    setActions((prev) => prev.map((a, idx) => (idx === i ? { ...a, ...patch } : a)));
  }
  function add() {
    setActions((prev) => [...prev, { ...EMPTY_ACTION }]);
  }
  function remove(i: number) {
    setActions((prev) => prev.filter((_, idx) => idx !== i));
  }

  async function submit() {
    setLastTx(null);
    try {
      const parsed = actions.map((a, i) => {
        let data: Record<string, unknown>;
        try {
          data = a.data.trim() ? JSON.parse(a.data) : {};
        } catch (e) {
          throw new Error(`Action #${i + 1} has invalid JSON: ${(e as Error).message}`);
        }
        if (!a.account.trim() || !a.name.trim()) {
          throw new Error(`Action #${i + 1}: contract and action name are required`);
        }
        return { account: a.account.trim(), name: a.name.trim(), data };
      });

      const res = await sign.mutateAsync({
        actions: parsed,
        broadcast,
      });
      setLastTx(res.transactionId);
      toast.success(broadcast ? "Transaction broadcast" : "Transaction signed");
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      toast.error(msg);
    }
  }

  const active = summary.data?.activeAccount;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <Wrench className="size-5 text-primary" />
          Utilities
        </h1>
        <p className="text-sm text-muted-foreground">
          Sign and broadcast arbitrary smart-contract actions with the active account
        </p>
      </div>

      {!active && (
        <Card>
          <CardContent className="flex items-center gap-3 py-4 text-sm">
            <AlertCircle className="size-5 text-destructive" />
            <p>No active account selected. Pick one from the sidebar first.</p>
          </CardContent>
        </Card>
      )}

      {actions.map((a, i) => (
        <Card key={i}>
          <CardHeader>
            <CardTitle className="flex items-center justify-between text-sm">
              <span>Action #{i + 1}</span>
              {actions.length > 1 && (
                <Button
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => remove(i)}
                  title="Remove"
                >
                  <Trash2 className="size-3.5" />
                </Button>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label>Contract</Label>
                <Input
                  value={a.account}
                  placeholder="eosio.token"
                  onChange={(e) => update(i, { account: e.target.value })}
                />
              </div>
              <div className="space-y-1">
                <Label>Action</Label>
                <Input
                  value={a.name}
                  placeholder="transfer"
                  onChange={(e) => update(i, { name: e.target.value })}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label>Data (JSON)</Label>
              <Textarea
                rows={6}
                spellCheck={false}
                className="font-mono text-xs"
                value={a.data}
                onChange={(e) => update(i, { data: e.target.value })}
              />
            </div>
          </CardContent>
        </Card>
      ))}

      <div className="flex flex-wrap items-center gap-3">
        <Button variant="outline" onClick={add}>
          <Plus className="size-4" />
          Add action
        </Button>

        <Separator orientation="vertical" className="h-6" />

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={broadcast}
            onChange={(e) => setBroadcast(e.target.checked)}
          />
          Broadcast after signing
        </label>

        <div className="ml-auto">
          <Button onClick={submit} disabled={sign.isPending || !active}>
            <Send className="size-4" />
            {sign.isPending ? "Signing…" : broadcast ? "Sign & Broadcast" : "Sign only"}
          </Button>
        </div>
      </div>

      {lastTx && (
        <Card className="border-primary/40">
          <CardContent className="space-y-1 py-4">
            <p className="text-xs uppercase text-muted-foreground">Last transaction</p>
            <p className="break-all font-mono text-xs">{lastTx}</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
