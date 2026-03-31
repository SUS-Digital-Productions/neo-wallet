import { useRef, useState } from "react";
import {
  Loader2,
  Check,
  Timer,
  Monitor,
  Radio,
  Copy,
  Download,
  Upload,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import {
  useAutoLockSettings,
  useAppSettings,
  useSetAutoLockSettings,
  useSetAppSettings,
  useEsrListenerStatus,
  useConnectEsrListener,
  useDisconnectEsrListener,
  useImportWallet,
} from "@/api/hooks";
import { exportWallet } from "@/api/client";
import { Input } from "@/components/ui/input";

const LOCK_OPTIONS = [
  { label: "15 minutes", value: 15 },
  { label: "30 minutes", value: 30 },
  { label: "1 hour", value: 60 },
  { label: "3 hours", value: 180 },
  { label: "8 hours", value: 480 },
  { label: "Never", value: 0 },
];

export default function Settings() {
  const { data: lockSettings } = useAutoLockSettings();
  const { data: appSettings } = useAppSettings();

  const setLock = useSetAutoLockSettings();
  const setApp = useSetAppSettings();
  const { data: esrListener } = useEsrListenerStatus({ refetchInterval: 10_000 });
  const connectListener = useConnectEsrListener();
  const disconnectListener = useDisconnectEsrListener();

  const importWalletMutation = useImportWallet();

  const [importPassword, setImportPassword] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const lockMinutes = lockSettings?.timeoutMinutes ?? 180;

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
          Manage application preferences and security
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
    </div>
  );
}
