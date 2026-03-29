import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowUpRight, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { AccountInfo, NetworkInfo } from "@/api/types";
import {
  getAccounts,
  getNetworks,
  getWalletSummary,
  sendTransfer,
} from "@/api/client";

export default function Send() {
  const navigate = useNavigate();

  const [accounts, setAccounts] = useState<AccountInfo[]>([]);
  const [networks, setNetworks] = useState<NetworkInfo[]>([]);
  const [from, setFrom] = useState("");
  const [chainId, setChainId] = useState("");
  const [to, setTo] = useState("");
  const [quantity, setQuantity] = useState("");
  const [memo, setMemo] = useState("");
  const [sending, setSending] = useState(false);

  useEffect(() => {
    async function init() {
      try {
        const [accs, nets, summary] = await Promise.all([
          getAccounts(),
          getNetworks(),
          getWalletSummary(),
        ]);
        setAccounts(accs);
        setNetworks(nets);
        if (summary.activeAccount) {
          setFrom(
            `${summary.activeAccount.account}@${summary.activeAccount.authority}`
          );
        }
        if (summary.activeNetwork) {
          setChainId(summary.activeNetwork.chainId);
        }
      } catch {
        /* empty – user can still fill fields manually */
      }
    }
    init();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!from || !to || !quantity || !chainId) return;

    const [account, authority] = from.split("@");
    setSending(true);
    try {
      const res = await sendTransfer({
        chainId,
        from: account,
        authority: authority ?? "active",
        to,
        quantity,
        memo: memo || undefined,
      });
      toast.success(`Transaction sent: ${res.transactionId.slice(0, 12)}…`);
      navigate("/");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Transfer failed");
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Send Tokens</h1>
        <p className="text-sm text-muted-foreground">
          Transfer tokens to another account
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ArrowUpRight className="size-4 text-primary" />
            Transfer
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Network */}
            <div className="space-y-2">
              <Label htmlFor="network">Network</Label>
              <Select value={chainId} onValueChange={setChainId}>
                <SelectTrigger id="network">
                  <SelectValue placeholder="Select network" />
                </SelectTrigger>
                <SelectContent>
                  {networks.map((n) => (
                    <SelectItem key={n.chainId} value={n.chainId}>
                      {n.name} ({n.symbol})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* From */}
            <div className="space-y-2">
              <Label htmlFor="from">From</Label>
              <Select value={from} onValueChange={setFrom}>
                <SelectTrigger id="from">
                  <SelectValue placeholder="Select account" />
                </SelectTrigger>
                <SelectContent>
                  {accounts.map((a) => (
                    <SelectItem
                      key={`${a.account}@${a.authority}`}
                      value={`${a.account}@${a.authority}`}
                    >
                      {a.account}@{a.authority}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* To */}
            <div className="space-y-2">
              <Label htmlFor="to">Recipient</Label>
              <Input
                id="to"
                placeholder="e.g. receiver1234"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                required
              />
            </div>

            {/* Quantity */}
            <div className="space-y-2">
              <Label htmlFor="qty">Quantity</Label>
              <Input
                id="qty"
                placeholder="e.g. 1.00000000 WAX"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                required
              />
            </div>

            {/* Memo */}
            <div className="space-y-2">
              <Label htmlFor="memo">Memo (optional)</Label>
              <Input
                id="memo"
                placeholder="Optional memo"
                value={memo}
                onChange={(e) => setMemo(e.target.value)}
              />
            </div>

            <Button type="submit" className="w-full" disabled={sending}>
              {sending && <Loader2 className="size-4 animate-spin" />}
              Send
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
