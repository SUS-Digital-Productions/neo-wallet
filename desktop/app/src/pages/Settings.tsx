import { useState } from "react";
import {
  Globe,
  Users,
  Loader2,
  Check,
  Timer,
  Monitor,
  UserX,
  ServerOff,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { EmptyState } from "@/components/EmptyState";
import type { AccountInfo } from "@/api/types";
import {
  useNetworks,
  useAccounts,
  useWalletSummary,
  useAutoLockSettings,
  useAppSettings,
  useSetActiveNetwork,
  useSetActiveAccount,
  useSetAutoLockSettings,
  useSetAppSettings,
} from "@/api/hooks";

const CHAIN_COLORS: Record<string, string> = {
  WAX: "bg-sus-wax/15 text-sus-wax border-sus-wax/30",
  EOS: "bg-sus-eos/15 text-sus-eos border-sus-eos/30",
  TLOS: "bg-sus-telos/15 text-sus-telos border-sus-telos/30",
};

const LOCK_OPTIONS = [
  { label: "15 minutes", value: 15 },
  { label: "30 minutes", value: 30 },
  { label: "1 hour", value: 60 },
  { label: "3 hours", value: 180 },
  { label: "8 hours", value: 480 },
  { label: "Never", value: 0 },
];

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

export default function Settings() {
  const { data: networks = [], isLoading: netsLoading } = useNetworks();
  const { data: accounts = [], isLoading: accsLoading } = useAccounts();
  const { data: summary } = useWalletSummary();
  const { data: lockSettings } = useAutoLockSettings();
  const { data: appSettings } = useAppSettings();

  const setNetwork = useSetActiveNetwork();
  const setAccount = useSetActiveAccount();
  const setLock = useSetAutoLockSettings();
  const setApp = useSetAppSettings();

  const [switching, setSwitching] = useState<string | null>(null);

  const activeNet = summary?.activeNetwork?.chainId ?? null;
  const activeAcc = summary?.activeAccount
    ? `${summary.activeAccount.account}@${summary.activeAccount.authority}@${summary.activeAccount.chainId}`
    : null;
  const lockMinutes = lockSettings?.timeoutMinutes ?? 180;

  const loading = netsLoading || accsLoading;

  function switchNetwork(chainId: string) {
    setSwitching(`net-${chainId}`);
    setNetwork.mutate(chainId, {
      onSuccess: () => toast.success("Network changed"),
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : "Failed to switch"),
      onSettled: () => setSwitching(null),
    });
  }

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

  function handleLockChange(minutes: number) {
    setLock.mutate(
      { timeoutMinutes: minutes },
      {
        onSuccess: () =>
          toast.success(
            minutes === 0
              ? "Auto-lock disabled"
              : `Auto-lock set to ${LOCK_OPTIONS.find((o) => o.value === minutes)?.label ?? minutes + " min"}`,
          ),
        onError: () => toast.error("Failed to update auto-lock"),
      },
    );
  }

  function handleStartAtLogin(checked: boolean) {
    setApp.mutate(
      {
        startAtLogin: checked,
        minimizeToTray: appSettings?.minimizeToTray ?? false,
      },
      {
        onSuccess: () =>
          toast.success(checked ? "Start at login enabled" : "Start at login disabled"),
        onError: () => toast.error("Failed to update setting"),
      },
    );
  }

  function handleMinimizeToTray(checked: boolean) {
    setApp.mutate(
      {
        startAtLogin: appSettings?.startAtLogin ?? false,
        minimizeToTray: checked,
      },
      {
        onSuccess: () =>
          toast.success(checked ? "Minimize to tray enabled" : "Minimize to tray disabled"),
        onError: () => toast.error("Failed to update setting"),
      },
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Settings</h1>
        <p className="text-sm text-muted-foreground">
          Manage networks, accounts, and security
        </p>
      </div>

      {/* Application */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Monitor className="size-4 text-primary" />
            Application
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="space-y-0.5">
              <Label htmlFor="startAtLogin">Start at login</Label>
              <p className="text-xs text-muted-foreground">
                Launch NeoWallet automatically when you log in
              </p>
            </div>
            <Switch
              id="startAtLogin"
              checked={appSettings?.startAtLogin ?? false}
              onCheckedChange={handleStartAtLogin}
              disabled={setApp.isPending}
            />
          </div>
          <Separator />
          <div className="flex items-center justify-between">
            <div className="space-y-0.5">
              <Label htmlFor="minimizeToTray">Minimize to tray</Label>
              <p className="text-xs text-muted-foreground">
                Keep the backend running when the window is closed
              </p>
            </div>
            <Switch
              id="minimizeToTray"
              checked={appSettings?.minimizeToTray ?? false}
              onCheckedChange={handleMinimizeToTray}
              disabled={setApp.isPending}
            />
          </div>
        </CardContent>
      </Card>

      {/* Auto-lock */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Timer className="size-4 text-primary" />
            Auto-Lock
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Automatically lock the wallet after a period of inactivity.
          </p>
          <div className="flex flex-wrap gap-2">
            {LOCK_OPTIONS.map((opt) => {
              const isActive = lockMinutes === opt.value;
              return (
                <Button
                  key={opt.value}
                  variant={isActive ? "default" : "outline"}
                  size="sm"
                  disabled={setLock.isPending}
                  onClick={() => handleLockChange(opt.value)}
                >
                  {isActive && <Check className="mr-1 size-3" />}
                  {opt.label}
                </Button>
              );
            })}
          </div>
        </CardContent>
      </Card>

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
            <EmptyState
              icon={ServerOff}
              title="No networks available"
              description="Networks will appear once the backend is configured"
            />
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
                    {chainBadge(n.symbol)}
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
            <EmptyState
              icon={UserX}
              title="No accounts imported"
              description="Import an account to get started"
            />
          ) : (
            accounts.map((a) => {
              const key = `${a.account}@${a.authority}@${a.chainId}`;
              const isActive = key === activeAcc;
              const isSwitching = switching === `acc-${key}`;
              const net = networks.find((n) => n.chainId === a.chainId);
              return (
                <div
                  key={key}
                  className="flex items-center justify-between rounded-lg border px-4 py-2"
                >
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
