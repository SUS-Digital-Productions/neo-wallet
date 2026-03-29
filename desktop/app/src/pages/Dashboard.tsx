import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowUpRight,
  ArrowDownLeft,
  RefreshCw,
  Wallet,
  Globe,
  Shield,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import type { WalletSummary, BalanceEntry } from "@/api/types";
import { getWalletSummary, getBalances } from "@/api/client";

export default function Dashboard() {
  const [summary, setSummary] = useState<WalletSummary | null>(null);
  const [balances, setBalances] = useState<BalanceEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const s = await getWalletSummary();
      setSummary(s);
      if (s.activeAccount && s.activeNetwork) {
        const b = await getBalances(
          s.activeAccount.account,
          s.activeNetwork.chainId
        );
        setBalances(b);
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to connect");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-20">
        <Shield className="size-12 text-muted-foreground" />
        <p className="text-lg font-medium">Backend unavailable</p>
        <p className="text-sm text-muted-foreground">{error}</p>
        <Button onClick={load}>Retry</Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            Wallet overview and quick actions
          </p>
        </div>
        <Button variant="outline" size="icon" onClick={load}>
          <RefreshCw className="size-4" />
        </Button>
      </div>

      {/* Account & Network info */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm">
              <Wallet className="size-4 text-primary" />
              Account
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <Skeleton className="h-5 w-32" />
            ) : (
              <div>
                <p className="text-lg font-semibold">
                  {summary?.activeAccount?.account ?? "No account"}
                </p>
                <p className="text-xs text-muted-foreground">
                  {summary?.activeAccount?.authority ?? "—"}
                </p>
              </div>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm">
              <Globe className="size-4 text-primary" />
              Network
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <Skeleton className="h-5 w-32" />
            ) : (
              <div>
                <p className="text-lg font-semibold">
                  {summary?.activeNetwork?.name ?? "—"}
                </p>
                <p className="text-xs text-muted-foreground font-mono">
                  {summary?.activeNetwork?.chainId?.slice(0, 16)}…
                </p>
              </div>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-sm">
              <Shield className="size-4 text-primary" />
              Status
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <Skeleton className="h-5 w-20" />
            ) : (
              <Badge
                variant={
                  summary?.listenerStatus === "Connected"
                    ? "default"
                    : "secondary"
                }
              >
                {summary?.listenerStatus ?? "Unknown"}
              </Badge>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Quick actions */}
      <div className="flex gap-3">
        <Button asChild>
          <Link to="/send">
            <ArrowUpRight className="size-4" />
            Send
          </Link>
        </Button>
        <Button variant="outline" asChild>
          <Link to="/receive">
            <ArrowDownLeft className="size-4" />
            Receive
          </Link>
        </Button>
      </div>

      <Separator />

      {/* Balances */}
      <div>
        <h2 className="mb-3 text-lg font-semibold">Portfolio</h2>
        {loading ? (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-14 w-full rounded-lg" />
            ))}
          </div>
        ) : balances.length === 0 ? (
          <Card>
            <CardContent className="py-8 text-center text-muted-foreground">
              No balances found
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-2">
            {balances.map((b) => (
              <Card key={b.symbol} className="py-3">
                <CardContent className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex size-9 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                      {b.symbol.slice(0, 2)}
                    </div>
                    <div>
                      <p className="font-medium">{b.symbol}</p>
                    </div>
                  </div>
                  <p className="font-mono text-sm">{b.amount}</p>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
