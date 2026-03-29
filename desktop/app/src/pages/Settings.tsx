import { useEffect, useState } from "react";
import { Globe, Users, Loader2, Check } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import type { AccountInfo, NetworkInfo } from "@/api/types";
import {
  getAccounts,
  getNetworks,
  getWalletSummary,
  setActiveAccount,
  setActiveNetwork,
} from "@/api/client";

export default function Settings() {
  const [networks, setNetworks] = useState<NetworkInfo[]>([]);
  const [accounts, setAccounts] = useState<AccountInfo[]>([]);
  const [activeNet, setActiveNet] = useState<string | null>(null);
  const [activeAcc, setActiveAcc] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [switching, setSwitching] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [nets, accs, summary] = await Promise.all([
          getNetworks(),
          getAccounts(),
          getWalletSummary(),
        ]);
        setNetworks(nets);
        setAccounts(accs);
        setActiveNet(summary.activeNetwork?.chainId ?? null);
        setActiveAcc(
          summary.activeAccount
            ? `${summary.activeAccount.account}@${summary.activeAccount.authority}`
            : null
        );
      } catch {
        /* silent */
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  async function switchNetwork(chainId: string) {
    setSwitching(`net-${chainId}`);
    try {
      await setActiveNetwork(chainId);
      setActiveNet(chainId);
      toast.success("Network changed");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to switch");
    } finally {
      setSwitching(null);
    }
  }

  async function switchAccount(acc: AccountInfo) {
    const key = `${acc.account}@${acc.authority}`;
    setSwitching(`acc-${key}`);
    try {
      await setActiveAccount({
        account: acc.account,
        authority: acc.authority,
        chainId: activeNet ?? "",
      });
      setActiveAcc(key);
      toast.success("Account changed");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to switch");
    } finally {
      setSwitching(null);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-sm text-muted-foreground">
          Manage networks and accounts
        </p>
      </div>

      {/* Networks */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Globe className="size-4 text-primary" />
            Networks
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {loading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-10 w-full rounded-lg" />
              ))}
            </div>
          ) : networks.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No networks available
            </p>
          ) : (
            networks.map((n) => {
              const isActive = n.chainId === activeNet;
              const isSwitching = switching === `net-${n.chainId}`;
              return (
                <div
                  key={n.chainId}
                  className="flex items-center justify-between rounded-lg border px-4 py-2"
                >
                  <div className="flex items-center gap-3">
                    <div>
                      <p className="text-sm font-medium">{n.name}</p>
                      <p className="font-mono text-xs text-muted-foreground">
                        {n.chainId.slice(0, 16)}…
                      </p>
                    </div>
                    {isActive && <Badge variant="default">Active</Badge>}
                  </div>
                  {!isActive && (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={isSwitching}
                      onClick={() => switchNetwork(n.chainId)}
                    >
                      {isSwitching ? (
                        <Loader2 className="size-3 animate-spin" />
                      ) : (
                        "Switch"
                      )}
                    </Button>
                  )}
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      <Separator />

      {/* Accounts */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Users className="size-4 text-primary" />
            Accounts
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {loading ? (
            <div className="space-y-2">
              {[1, 2].map((i) => (
                <Skeleton key={i} className="h-10 w-full rounded-lg" />
              ))}
            </div>
          ) : accounts.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No accounts found
            </p>
          ) : (
            accounts.map((a) => {
              const key = `${a.account}@${a.authority}`;
              const isActive = key === activeAcc;
              const isSwitching = switching === `acc-${key}`;
              return (
                <div
                  key={key}
                  className="flex items-center justify-between rounded-lg border px-4 py-2"
                >
                  <div className="flex items-center gap-3">
                    <div>
                      <p className="text-sm font-medium">{key}</p>
                      <p className="font-mono text-xs text-muted-foreground">
                        {a.publicKey.slice(0, 24)}…
                      </p>
                    </div>
                    {isActive && (
                      <Badge variant="default">
                        <Check className="mr-1 size-3" />
                        Active
                      </Badge>
                    )}
                  </div>
                  {!isActive && (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={isSwitching}
                      onClick={() => switchAccount(a)}
                    >
                      {isSwitching ? (
                        <Loader2 className="size-3 animate-spin" />
                      ) : (
                        "Use"
                      )}
                    </Button>
                  )}
                </div>
              );
            })
          )}
        </CardContent>
      </Card>
    </div>
  );
}
