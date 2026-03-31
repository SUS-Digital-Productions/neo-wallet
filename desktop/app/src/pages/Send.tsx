import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { ArrowUpRight, Loader2, Users } from "lucide-react";
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
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/EmptyState";
import { useAccounts, useNetworks, useWalletSummary, useSendTransfer } from "@/api/hooks";

export default function Send() {
  const navigate = useNavigate();

  const accounts = useAccounts();
  const networks = useNetworks();
  const summary = useWalletSummary();
  const transfer = useSendTransfer();

  const [from, setFrom] = useState("");
  const [chainId, setChainId] = useState("");
  const [to, setTo] = useState("");
  const [quantity, setQuantity] = useState("");
  const [memo, setMemo] = useState("");

  if (!from && summary.data?.activeAccount) {
    const a = summary.data.activeAccount;
    setFrom(`${a.account}@${a.authority}`);
  }
  if (!chainId && summary.data?.activeNetwork) {
    setChainId(summary.data.activeNetwork.chainId);
  }

  const loading = accounts.isLoading || networks.isLoading || summary.isLoading;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!from || !to || !quantity || !chainId) return;

    const [account, authority] = from.split("@");
    transfer.mutate(
      {
        chainId,
        from: account,
        authority: authority ?? "active",
        to,
        quantity,
        memo: memo || undefined,
      },
      {
        onSuccess: (res) => {
          toast.success(`Transaction sent: ${res.transactionId.slice(0, 12)}…`);
          navigate("/");
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : "Transfer failed");
        },
      },
    );
  }

  if (loading) {
    return (
      <div className="mx-auto max-w-lg space-y-6">
        <div>
          <h1 className="text-2xl font-semibold">Send Tokens</h1>
          <p className="text-sm text-muted-foreground">
            Transfer tokens to another account
          </p>
        </div>
        <Card>
          <CardContent className="space-y-4 pt-6">
            {[1, 2, 3, 4, 5].map((i) => (
              <Skeleton key={i} className="h-10 w-full rounded-lg" />
            ))}
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!accounts.isLoading && (!accounts.data || accounts.data.length === 0)) {
    return (
      <div className="mx-auto max-w-lg space-y-6">
        <div>
          <h1 className="text-2xl font-semibold">Send Tokens</h1>
          <p className="text-sm text-muted-foreground">
            Transfer tokens to another account
          </p>
        </div>
        <EmptyState
          icon={Users}
          title="No accounts imported"
          description="Import an account first to send tokens"
          action={
            <Button variant="outline" size="sm" asChild>
              <Link to="/import">Import Account</Link>
            </Button>
          }
        />
      </div>
    );
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
                  {(networks.data ?? []).map((n) => (
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
                  {(accounts.data ?? []).map((a) => (
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

            <Button type="submit" className="w-full" disabled={transfer.isPending}>
              {transfer.isPending && <Loader2 className="size-4 animate-spin" />}
              Send
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
