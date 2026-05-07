import { Link } from "react-router-dom";
import {
  ArrowUpRight,
  ArrowDownLeft,
  RefreshCw,
  Wallet,
  Globe,
  Shield,
  Coins,
  ChevronDown,
  Check,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { EmptyState } from "@/components/EmptyState";
import {
  useWalletSummary,
  useBalances,
  useAccounts,
  useSetActiveAccount,
} from "@/api/hooks";
import { useState } from "react";

export default function Dashboard() {
  const summary = useWalletSummary();
  const { data: accounts = [] } = useAccounts();
  const setAccount = useSetActiveAccount();
  const [showAccountPicker, setShowAccountPicker] = useState(false);

  const activeChainId = summary.data?.activeNetwork?.chainId;
  const networkAccounts = accounts.filter((a) => a.chainId === activeChainId);
  const balances = useBalances(
    summary.data?.activeAccount?.account,
    summary.data?.activeAccount?.chainId,
  );

  const loading = summary.isLoading;

  function refetch() {
    summary.refetch();
    balances.refetch();
  }

  if (summary.error) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-20">
        <Shield className="size-12 text-muted-foreground" />
        <p className="text-lg font-medium">Backend unavailable</p>
        <p className="text-sm text-muted-foreground">
          {summary.error instanceof Error ? summary.error.message : "Failed to connect"}
        </p>
        <Button onClick={() => summary.refetch()}>Retry</Button>
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
        <Button variant="outline" size="icon" onClick={refetch} disabled={summary.isFetching || balances.isFetching}>
          <RefreshCw className={`size-4 ${summary.isFetching || balances.isFetching ? "animate-spin" : ""}`} />
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
              <div className="space-y-2">
                <button
                  type="button"
                  onClick={() => setShowAccountPicker((v) => !v)}
                  className="flex w-full items-center justify-between rounded-md text-left"
                >
                  <div>
                    <div className="flex items-center gap-2">
                      <p className="text-lg font-semibold">
                        {summary.data?.activeAccount?.account ?? "No account"}
                      </p>
                      {summary.data?.activeAccount?.chainName && (
                        <span className="text-xs text-muted-foreground">
                          ({summary.data.activeAccount.chainName})
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-muted-foreground">
                      {summary.data?.activeAccount?.authority ?? "—"}
                    </p>
                  </div>
                  {networkAccounts.length > 1 && (
                    <ChevronDown
                      className={`size-4 text-muted-foreground transition-transform ${
                        showAccountPicker ? "rotate-180" : ""
                      }`}
                    />
                  )}
                </button>

                {showAccountPicker && networkAccounts.length > 1 && (
                  <div className="space-y-1 rounded-md border p-1">
                    {networkAccounts.map((a) => {
                      const isActive =
                        summary.data?.activeAccount?.account === a.account &&
                        summary.data?.activeAccount?.authority === a.authority &&
                        summary.data?.activeAccount?.chainId === a.chainId;
                      return (
                        <button
                          key={`${a.account}@${a.authority}@${a.chainId}`}
                          type="button"
                          disabled={isActive || setAccount.isPending}
                          onClick={() => {
                            setAccount.mutate(
                              { account: a.account, authority: a.authority, chainId: a.chainId },
                              {
                                onSuccess: () => {
                                  toast.success(`Switched to ${a.account}`);
                                  setShowAccountPicker(false);
                                },
                                onError: () => toast.error("Failed to switch account"),
                              },
                            );
                          }}
                          className={`flex w-full items-center justify-between rounded px-2 py-1.5 text-left text-sm transition-colors ${
                            isActive
                              ? "bg-primary/10 text-primary"
                              : "hover:bg-accent"
                          }`}
                        >
                          <div>
                            <span className="font-medium">{a.account}</span>
                            <span className="ml-1 text-muted-foreground">@{a.authority}</span>
                          </div>
                          {isActive && <Check className="size-3.5" />}
                        </button>
                      );
                    })}
                  </div>
                )}
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
                  {summary.data?.activeNetwork?.name ?? "—"}
                </p>
                <p className="text-xs text-muted-foreground font-mono">
                  {summary.data?.activeNetwork?.chainId?.slice(0, 16)}…
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
                  summary.data?.listenerStatus === "Connected"
                    ? "default"
                    : "secondary"
                }
              >
                {summary.data?.listenerStatus ?? "Unknown"}
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
        {loading || balances.isLoading ? (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-14 w-full rounded-lg" />
            ))}
          </div>
        ) : !balances.data || balances.data.length === 0 ? (
          <EmptyState
            icon={Coins}
            title="No balances found"
            description={
              summary.data?.activeAccount
                ? "This account has no token balances yet"
                : "Import an account to view balances"
            }
            action={
              !summary.data?.activeAccount ? (
                <Button variant="outline" size="sm" asChild>
                  <Link to="/keys">Import Account</Link>
                </Button>
              ) : undefined
            }
          />
        ) : (
          <div className="space-y-2">
            {balances.data.map((b) => (
              <Card key={`${b.contract}:${b.symbol}`} className="py-3">
                <CardContent className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex size-9 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                      {b.symbol.slice(0, 2)}
                    </div>
                    <div>
                      <p className="font-medium">{b.symbol}</p>
                      <p className="font-mono text-xs text-muted-foreground">
                        {b.contract}
                      </p>
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
