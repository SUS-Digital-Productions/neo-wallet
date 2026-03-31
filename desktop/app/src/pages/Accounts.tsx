import { useState } from "react";
import {
  Users,
  Loader2,
  Check,
  UserX,
  Copy,
  Eye,
  EyeOff,
  KeyRound,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/EmptyState";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import type { AccountInfo } from "@/api/types";
import {
  useAccounts,
  useNetworks,
  useWalletSummary,
  useSetActiveAccount,
  useGetPrivateKey,
  useRemoveAccount,
} from "@/api/hooks";

const CHAIN_COLORS: Record<string, string> = {
  WAX: "bg-sus-wax/15 text-sus-wax border-sus-wax/30",
  EOS: "bg-sus-eos/15 text-sus-eos border-sus-eos/30",
  TLOS: "bg-sus-telos/15 text-sus-telos border-sus-telos/30",
};

function chainBadge(symbol: string) {
  const cls = CHAIN_COLORS[symbol] ?? "bg-muted text-muted-foreground";
  return (
    <span
      className={`inline-flex items-center rounded-md border px-2 py-0.5 text-xs font-semibold ${cls}`}
    >
      {symbol}
    </span>
  );
}

export default function Accounts() {
  const { data: accounts = [], isLoading } = useAccounts();
  const { data: networks = [] } = useNetworks();
  const { data: summary } = useWalletSummary();
  const setAccount = useSetActiveAccount();
  const getPrivateKey = useGetPrivateKey();
  const removeAccountMutation = useRemoveAccount();

  const [switching, setSwitching] = useState<string | null>(null);
  const [revealedKey, setRevealedKey] = useState<{ id: string; wif: string } | null>(null);
  const [removeTarget, setRemoveTarget] = useState<{
    account: string;
    authority: string;
    chainId: string;
    symbol: string;
  } | null>(null);

  const activeAcc = summary?.activeAccount
    ? `${summary.activeAccount.account}@${summary.activeAccount.authority}@${summary.activeAccount.chainId}`
    : null;

  function switchAccount(acc: AccountInfo) {
    const key = `${acc.account}@${acc.authority}@${acc.chainId}`;
    setSwitching(`acc-${key}`);
    setAccount.mutate(
      { account: acc.account, authority: acc.authority, chainId: acc.chainId },
      {
        onSuccess: () => toast.success("Account changed"),
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Failed to switch"),
        onSettled: () => setSwitching(null),
      },
    );
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Accounts</h1>
        <p className="text-sm text-muted-foreground">
          Manage imported blockchain accounts
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Users className="size-4 text-primary" />
            Imported Accounts
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {isLoading ? (
            <div className="space-y-2">
              {[1, 2].map((i) => (
                <Skeleton key={i} className="h-10 w-full rounded-lg" />
              ))}
            </div>
          ) : accounts.length === 0 ? (
            <EmptyState
              icon={UserX}
              title="No accounts imported"
              description="Add a key and import accounts from the Keys page"
            />
          ) : (
            accounts.map((a) => {
              const key = `${a.account}@${a.authority}@${a.chainId}`;
              const isActive = key === activeAcc;
              const isSwitching = switching === `acc-${key}`;
              const net = networks.find((n) => n.chainId === a.chainId);
              return (
                <div key={key} className="space-y-0">
                  <div className="flex items-center justify-between rounded-lg border px-4 py-2">
                    <div className="flex items-center gap-3">
                      {net && chainBadge(net.symbol)}
                      <div>
                        <p className="text-sm font-medium">
                          {a.account}
                          <span className="ml-1 text-muted-foreground">
                            @{a.authority}
                          </span>
                        </p>
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
                    <div className="flex items-center gap-2">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-8"
                        disabled={getPrivateKey.isPending}
                        onClick={() => {
                          const id = `${a.account}@${a.authority}`;
                          if (revealedKey?.id === id) {
                            setRevealedKey(null);
                            return;
                          }
                          getPrivateKey.mutate(
                            { account: a.account, authority: a.authority },
                            {
                              onSuccess: (res) =>
                                setRevealedKey({ id, wif: res.privateKey }),
                              onError: () =>
                                toast.error("Failed to retrieve private key"),
                            },
                          );
                        }}
                      >
                        {revealedKey?.id === `${a.account}@${a.authority}` ? (
                          <EyeOff className="size-3.5" />
                        ) : (
                          <Eye className="size-3.5" />
                        )}
                      </Button>
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
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-8 text-destructive hover:text-destructive"
                        disabled={removeAccountMutation.isPending}
                        onClick={() =>
                          setRemoveTarget({
                            account: a.account,
                            authority: a.authority,
                            chainId: a.chainId,
                            symbol: net?.symbol ?? "",
                          })
                        }
                      >
                        <Trash2 className="size-3.5" />
                      </Button>
                    </div>
                  </div>
                  {revealedKey?.id === `${a.account}@${a.authority}` && (
                    <div className="mt-2 flex items-center gap-2 rounded-md bg-muted px-3 py-2">
                      <KeyRound className="size-3.5 shrink-0 text-muted-foreground" />
                      <code className="flex-1 break-all text-xs">
                        {revealedKey.wif}
                      </code>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-7"
                        onClick={() => {
                          navigator.clipboard.writeText(revealedKey.wif);
                          toast.success("Private key copied");
                        }}
                      >
                        <Copy className="size-3" />
                      </Button>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={removeTarget !== null}
        onOpenChange={(open) => { if (!open) setRemoveTarget(null); }}
        title="Remove Account"
        description={
          removeTarget
            ? `Are you sure you want to remove ${removeTarget.account}@${removeTarget.authority}${removeTarget.symbol ? ` (${removeTarget.symbol})` : ""}? The private key will remain in your wallet.`
            : ""
        }
        confirmLabel="Remove"
        onConfirm={() => {
          if (!removeTarget) return;
          removeAccountMutation.mutate(
            {
              account: removeTarget.account,
              authority: removeTarget.authority,
              chainId: removeTarget.chainId,
            },
            {
              onSuccess: () => toast.success("Account removed"),
              onError: () => toast.error("Failed to remove account"),
            },
          );
        }}
        disabled={removeAccountMutation.isPending}
      />
    </div>
  );
}
