import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  RefreshCw,
  User,
  Cpu,
  Wifi,
  HardDrive,
  Coins,
  KeyRound,
  Vote,
  Search,
  AlertCircle,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { EmptyState } from "@/components/EmptyState";
import {
  useWalletSummary,
  useChainAccount,
  useBalances,
} from "@/api/hooks";

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes)) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function formatMicroseconds(us: number): string {
  if (!Number.isFinite(us)) return "—";
  if (us < 1000) return `${us} µs`;
  if (us < 1_000_000) return `${(us / 1000).toFixed(2)} ms`;
  return `${(us / 1_000_000).toFixed(2)} s`;
}

function ResourceBar({ used, max }: { used: number; max: number }) {
  const pct = max > 0 ? Math.min(100, (used / max) * 100) : 0;
  const color = pct > 90 ? "bg-destructive" : pct > 70 ? "bg-yellow-500" : "bg-primary";
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
      <div className={`h-full ${color}`} style={{ width: `${pct}%` }} />
    </div>
  );
}

export default function Account() {
  const [params, setParams] = useSearchParams();
  const summary = useWalletSummary();
  const activeAccount = summary.data?.activeAccount;
  const chainId = summary.data?.activeNetwork?.chainId;

  // ?name=foo lets you view any other account from a deep-link
  const queryName = params.get("name");
  const accountName = queryName || activeAccount?.account;
  const [search, setSearch] = useState(queryName ?? "");

  const acct = useChainAccount(accountName, chainId);
  const bals = useBalances(accountName, chainId);

  function refresh() {
    acct.refetch();
    bals.refetch();
  }

  function viewAccount(name: string) {
    const trimmed = name.trim();
    if (!trimmed) return;
    setParams({ name: trimmed });
  }

  function clearLookup() {
    setParams({});
    setSearch("");
  }

  const data = acct.data;
  const isLookup = !!queryName && queryName !== activeAccount?.account;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Account</h1>
          <p className="text-sm text-muted-foreground">
            On-chain details, resources, permissions and balances
          </p>
        </div>
        <Button
          variant="outline"
          size="icon"
          onClick={refresh}
          disabled={acct.isFetching || bals.isFetching}
        >
          <RefreshCw
            className={`size-4 ${acct.isFetching || bals.isFetching ? "animate-spin" : ""}`}
          />
        </Button>
      </div>

      {/* Lookup other accounts */}
      <Card>
        <CardContent className="flex flex-col gap-2 sm:flex-row">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") viewAccount(search);
              }}
              placeholder="Look up any account on this chain (e.g. eosio)"
              className="pl-8"
            />
          </div>
          <div className="flex gap-2">
            <Button onClick={() => viewAccount(search)}>View</Button>
            {isLookup && (
              <Button variant="outline" onClick={clearLookup}>
                My account
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {!accountName ? (
        <EmptyState
          icon={User}
          title="No active account"
          description="Import or look up an account to view details"
        />
      ) : acct.error ? (
        <Card>
          <CardContent className="flex items-center gap-3 py-6 text-sm">
            <AlertCircle className="size-5 text-destructive" />
            <div className="flex-1">
              <p className="font-medium">Failed to load account</p>
              <p className="text-xs text-muted-foreground">
                {acct.error instanceof Error ? acct.error.message : String(acct.error)}
              </p>
            </div>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Header card */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center justify-between text-base">
                <span className="flex items-center gap-2">
                  <User className="size-4 text-primary" />
                  {accountName}
                </span>
                {isLookup && <Badge variant="secondary">Lookup</Badge>}
              </CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <Field label="Created" value={data?.created?.split("T")[0] ?? "—"} loading={acct.isLoading} />
              <Field
                label="Liquid"
                value={data?.core_liquid_balance ?? "0"}
                loading={acct.isLoading}
              />
              <Field
                label="Staked (self)"
                value={
                  data?.self_delegated_bandwidth
                    ? `${data.self_delegated_bandwidth.cpu_weight} + ${data.self_delegated_bandwidth.net_weight}`
                    : "—"
                }
                loading={acct.isLoading}
              />
            </CardContent>
          </Card>

          {/* Resources */}
          <div className="grid gap-4 lg:grid-cols-3">
            <ResourceCard
              icon={<Cpu className="size-4 text-primary" />}
              label="CPU"
              loading={acct.isLoading}
              used={data?.cpu_limit.used ?? 0}
              max={data?.cpu_limit.max ?? 0}
              format={formatMicroseconds}
            />
            <ResourceCard
              icon={<Wifi className="size-4 text-primary" />}
              label="NET"
              loading={acct.isLoading}
              used={data?.net_limit.used ?? 0}
              max={data?.net_limit.max ?? 0}
              format={formatBytes}
            />
            <ResourceCard
              icon={<HardDrive className="size-4 text-primary" />}
              label="RAM"
              loading={acct.isLoading}
              used={data?.ram_usage ?? 0}
              max={data?.ram_quota ?? 0}
              format={formatBytes}
            />
          </div>

          {/* Refund */}
          {data?.refund_request && (
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Pending Refund</CardTitle>
              </CardHeader>
              <CardContent className="grid grid-cols-3 gap-4 text-sm">
                <Field label="CPU" value={data.refund_request.cpu_amount} />
                <Field label="NET" value={data.refund_request.net_amount} />
                <Field
                  label="Available at"
                  value={data.refund_request.request_time.split("T")[0]}
                />
              </CardContent>
            </Card>
          )}

          {/* Voter info */}
          {data?.voter_info && (data.voter_info.proxy || data.voter_info.producers?.length) && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-sm">
                  <Vote className="size-4 text-primary" />
                  Voting
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {data.voter_info.proxy ? (
                  <div>
                    <span className="text-muted-foreground">Proxy: </span>
                    <span className="font-mono">{data.voter_info.proxy}</span>
                  </div>
                ) : (
                  <div>
                    <p className="mb-1 text-xs text-muted-foreground">Producers</p>
                    <div className="flex flex-wrap gap-1">
                      {data.voter_info.producers.map((p) => (
                        <Badge key={p} variant="secondary" className="font-mono text-[10px]">
                          {p}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Permissions */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-sm">
                <KeyRound className="size-4 text-primary" />
                Permissions
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {acct.isLoading ? (
                <Skeleton className="h-20 w-full" />
              ) : (
                data?.permissions.map((p) => (
                  <div key={p.perm_name} className="rounded-md border p-3 text-sm">
                    <div className="mb-2 flex items-center gap-2">
                      <Badge>{p.perm_name}</Badge>
                      {p.parent && (
                        <span className="text-xs text-muted-foreground">
                          parent: <span className="font-mono">{p.parent}</span>
                        </span>
                      )}
                      <span className="ml-auto text-xs text-muted-foreground">
                        threshold {p.required_auth.threshold}
                      </span>
                    </div>
                    {p.required_auth.keys.map((k) => (
                      <div key={k.key} className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            navigator.clipboard.writeText(k.key);
                            toast.success("Public key copied");
                          }}
                          className="break-all font-mono text-[11px] hover:text-primary"
                          title="Click to copy"
                        >
                          {k.key}
                        </button>
                        <span className="text-[10px] text-muted-foreground">
                          weight {k.weight}
                        </span>
                      </div>
                    ))}
                    {p.required_auth.accounts.map((a, i) => (
                      <div key={i} className="text-xs">
                        <span className="font-mono">
                          {a.permission.actor}@{a.permission.permission}
                        </span>{" "}
                        <span className="text-muted-foreground">weight {a.weight}</span>
                      </div>
                    ))}
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          {/* Balances */}
          <div>
            <h2 className="mb-2 flex items-center gap-2 text-base font-semibold">
              <Coins className="size-4 text-primary" />
              Token balances
            </h2>
            <Separator className="mb-3" />
            {bals.isLoading ? (
              <Skeleton className="h-14 w-full" />
            ) : !bals.data || bals.data.length === 0 ? (
              <p className="text-sm text-muted-foreground">No tokens found.</p>
            ) : (
              <div className="space-y-2">
                {bals.data.map((b) => (
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
        </>
      )}
    </div>
  );
}

function Field({
  label,
  value,
  loading,
}: {
  label: string;
  value: string;
  loading?: boolean;
}) {
  return (
    <div>
      <p className="text-xs uppercase text-muted-foreground">{label}</p>
      {loading ? (
        <Skeleton className="mt-1 h-5 w-24" />
      ) : (
        <p className="font-mono text-sm">{value}</p>
      )}
    </div>
  );
}

function ResourceCard({
  icon,
  label,
  used,
  max,
  loading,
  format,
}: {
  icon: React.ReactNode;
  label: string;
  used: number;
  max: number;
  loading?: boolean;
  format: (n: number) => string;
}) {
  const pct = max > 0 ? Math.min(100, (used / max) * 100) : 0;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-sm">
          {icon}
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {loading ? (
          <Skeleton className="h-12 w-full" />
        ) : (
          <>
            <div className="flex items-baseline justify-between text-sm">
              <span className="font-mono">{format(used)}</span>
              <span className="text-xs text-muted-foreground">/ {format(max)}</span>
            </div>
            <ResourceBar used={used} max={max} />
            <p className="text-xs text-muted-foreground">{pct.toFixed(1)}% used</p>
          </>
        )}
      </CardContent>
    </Card>
  );
}
