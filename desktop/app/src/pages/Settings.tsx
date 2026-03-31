import { useRef, useState } from "react";
import {
  Globe,
  Users,
  Loader2,
  Check,
  Timer,
  Monitor,
  UserX,
  ServerOff,
  Radio,
  Copy,
  Eye,
  EyeOff,
  Download,
  Upload,
  KeyRound,
  Trash2,
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
  useEsrListenerStatus,
  useConnectEsrListener,
  useDisconnectEsrListener,
  useGetPrivateKey,
  useImportWallet,
  useRemoveAccount,
} from "@/api/hooks";
import { exportWallet } from "@/api/client";
import { Input } from "@/components/ui/input";
import { ConfirmDialog } from "@/components/ConfirmDialog";

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
  const { data: esrListener } = useEsrListenerStatus({ refetchInterval: 10_000 });
  const connectListener = useConnectEsrListener();
  const disconnectListener = useDisconnectEsrListener();

  const getPrivateKey = useGetPrivateKey();
  const importWalletMutation = useImportWallet();
  const removeAccountMutation = useRemoveAccount();

  const [switching, setSwitching] = useState<string | null>(null);
  const [revealedKey, setRevealedKey] = useState<{ id: string; wif: string } | null>(null);
  const [importPassword, setImportPassword] = useState("");
  const [removeTarget, setRemoveTarget] = useState<{
    account: string;
    authority: string;
    chainId: string;
    symbol: string;
  } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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

      {/* ESR Listener */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Radio className="size-4 text-primary" />
            Anchor Link Listener
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div
                className={`size-2 rounded-full ${esrListener?.status === "Connected" ? "bg-green-500" : "bg-muted-foreground"}`}
              />
              <span className="text-sm font-medium">
                {esrListener?.status ?? "Unknown"}
              </span>
              <Badge variant="secondary" className="text-[10px]">
                {esrListener?.sessionCount ?? 0} sessions
              </Badge>
            </div>
            {esrListener?.status === "Connected" ? (
              <Button
                variant="outline"
                size="sm"
                disabled={disconnectListener.isPending}
                onClick={() =>
                  disconnectListener.mutate(undefined, {
                    onSuccess: () => toast.success("Listener disconnected"),
                    onError: () => toast.error("Failed to disconnect"),
                  })
                }
              >
                {disconnectListener.isPending && (
                  <Loader2 className="mr-1 size-3 animate-spin" />
                )}
                Disconnect
              </Button>
            ) : (
              <Button
                variant="outline"
                size="sm"
                disabled={connectListener.isPending}
                onClick={() =>
                  connectListener.mutate(undefined, {
                    onSuccess: () => toast.success("Listener connected"),
                    onError: () => toast.error("Failed to connect"),
                  })
                }
              >
                {connectListener.isPending && (
                  <Loader2 className="mr-1 size-3 animate-spin" />
                )}
                Connect
              </Button>
            )}
          </div>
          {esrListener?.linkId && (
            <>
              <Separator />
              <div className="space-y-2">
                <div>
                  <p className="text-xs font-medium text-muted-foreground">
                    Link ID
                  </p>
                  <div className="flex items-center gap-2">
                    <code className="flex-1 truncate rounded bg-muted px-2 py-1 text-xs">
                      {esrListener.linkId}
                    </code>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="size-7"
                      onClick={() => {
                        navigator.clipboard.writeText(esrListener.linkId);
                        toast.success("Link ID copied");
                      }}
                    >
                      <Copy className="size-3" />
                    </Button>
                  </div>
                </div>
                {esrListener.requestPublicKey && (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground">
                      Request Public Key
                    </p>
                    <code className="block truncate rounded bg-muted px-2 py-1 text-xs">
                      {esrListener.requestPublicKey}
                    </code>
                  </div>
                )}
                <p className="text-xs text-muted-foreground">
                  dApps learn these values when you approve an identity request.
                  Future signing requests use this channel automatically.
                </p>
              </div>
            </>
          )}
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
                <div key={key} className="space-y-0">
                  <div
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
                          }
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

      {/* ── Wallet Backup ─────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Download className="size-4 text-primary" />
            Wallet Backup
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Export */}
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium">Export Wallet</p>
              <p className="text-xs text-muted-foreground">
                Download your encrypted wallet file
              </p>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={async () => {
                try {
                  const blob = await exportWallet();
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement("a");
                  a.href = url;
                  a.download = "wallet.json";
                  a.click();
                  URL.revokeObjectURL(url);
                  toast.success("Wallet exported");
                } catch {
                  toast.error("Failed to export wallet");
                }
              }}
            >
              <Download className="mr-1 size-3.5" />
              Export
            </Button>
          </div>

          <Separator />

          {/* Import */}
          <div>
            <p className="text-sm font-medium">Import Wallet</p>
            <p className="mb-3 text-xs text-muted-foreground">
              Restore from an encrypted wallet backup file
            </p>
            <div className="space-y-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => fileInputRef.current?.click()}
              >
                <Upload className="mr-1 size-3.5" />
                Choose File
              </Button>
              <input
                ref={fileInputRef}
                type="file"
                accept=".json"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  toast.info(`Selected: ${file.name}`);
                }}
              />
              <Input
                type="password"
                placeholder="Wallet password"
                value={importPassword}
                onChange={(e) => setImportPassword(e.target.value)}
              />
              <Button
                size="sm"
                disabled={
                  importWalletMutation.isPending ||
                  !importPassword ||
                  !fileInputRef.current?.files?.length
                }
                onClick={async () => {
                  const file = fileInputRef.current?.files?.[0];
                  if (!file) return;
                  const buf = await file.arrayBuffer();
                  const base64 = btoa(
                    String.fromCharCode(...new Uint8Array(buf))
                  );
                  importWalletMutation.mutate(
                    { password: importPassword, fileBase64: base64 },
                    {
                      onSuccess: () => {
                        toast.success("Wallet imported successfully");
                        setImportPassword("");
                        if (fileInputRef.current) fileInputRef.current.value = "";
                      },
                      onError: () => toast.error("Failed to import wallet"),
                    }
                  );
                }}
              >
                {importWalletMutation.isPending ? (
                  <Loader2 className="mr-1 size-3.5 animate-spin" />
                ) : (
                  <Upload className="mr-1 size-3.5" />
                )}
                Import
              </Button>
            </div>
          </div>
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
