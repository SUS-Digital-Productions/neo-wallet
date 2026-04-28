import { Outlet, NavLink, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import {
  LayoutDashboard,
  Send,
  QrCode,
  Settings,
  Wallet,
  LogOut,
  FileCheck,
  Radio,
  KeyRound,
  Globe,
  Users,
  User,
  Cpu,
  HardDrive,
  Zap,
  Vote,
  Wrench,
  ScrollText,
  ShieldCheck,
  UserPlus,
  Clock,
} from "lucide-react";
import { AccountSwitcher } from "@/components/AccountSwitcher";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  useLockWallet,
  useWalletSummary,
  useAccounts,
  useSetActiveAccount,
} from "@/api/hooks";
import { useEsrEvents } from "@/api/useEsrEvents";
import { useEsrDeepLink } from "@/api/useEsrDeepLink";
import type { EsrSseEvent } from "@/api/types";

const navItems = [
  { to: "/", icon: LayoutDashboard, label: "Dashboard" },
  { to: "/account", icon: User, label: "Account" },
  { to: "/send", icon: Send, label: "Transfer" },
  { to: "/receive", icon: QrCode, label: "Receive" },
  { to: "/resources", icon: Cpu, label: "CPU / NET" },
  { to: "/ram", icon: HardDrive, label: "RAM" },
  { to: "/powerup", icon: Zap, label: "PowerUp" },
  { to: "/vote", icon: Vote, label: "Vote" },
  { to: "/permissions", icon: ShieldCheck, label: "Permissions" },
  { to: "/create-account", icon: UserPlus, label: "Create Account" },
  { to: "/utilities", icon: Wrench, label: "Utilities" },
  { to: "/esr", icon: FileCheck, label: "Sign" },
  { to: "/keys", icon: KeyRound, label: "Keys" },
  { to: "/accounts", icon: Users, label: "Accounts" },
  { to: "/networks", icon: Globe, label: "Networks" },
  { to: "/msig", icon: ScrollText, label: "MSIG" },
  { to: "/settings", icon: Settings, label: "Settings" },
];

export default function Layout() {
  const navigate = useNavigate();
  const lock = useLockWallet();
  const { data: summary } = useWalletSummary({ refetchInterval: 10_000 });
  const { data: accounts = [] } = useAccounts();
  const setActiveAccount = useSetActiveAccount();

  const listenerConnected = summary?.listenerStatus === "Connected";

  // Listen for ESR deep links from Tauri (esr:// protocol handler)
  useEsrDeepLink();

  // Listen for ESR signing requests from the background listener
  useEsrEvents((evt: EsrSseEvent) => {
    if (evt.type !== "signing_request") return;
    const sigEvt = evt as import("@/api/types").EsrSigningRequestEvent;

    // ── Auto-route to the wallet that should sign this request ───────────
    // The session payload (when present) tells us actor/permission/chainId.
    // If we hold a matching account, switch to it before navigating so the
    // approval page is pre-populated. Otherwise prompt the user to import.
    const session = sigEvt.session;
    if (session && accounts.length > 0) {
      const match = accounts.find(
        (a) =>
          a.account === session.actor &&
          a.authority === session.permission &&
          a.chainId === session.chainId,
      );

      if (match) {
        const isActive =
          summary?.activeAccount?.account === match.account &&
          summary?.activeAccount?.authority === match.authority &&
          summary?.activeAccount?.chainId === match.chainId;
        if (!isActive) {
          setActiveAccount.mutate({
            account: match.account,
            authority: match.authority,
            chainId: match.chainId,
          });
        }
        toast.info(`Signing request for ${match.account}@${match.authority}`);
      } else {
        toast.warning(
          `Signing request for ${session.actor}@${session.permission} — that account isn't in your wallet. Import it first.`,
          { duration: 8_000 },
        );
        navigate("/keys");
        return;
      }
    } else {
      toast.info("Signing request received from dApp");
    }

    // Navigate to the approval page
    if (sigEvt.requestId) {
      navigate(`/esr?requestId=${sigEvt.requestId}`);
    } else if ("rawPayload" in sigEvt && sigEvt.rawPayload) {
      navigate(`/esr?uri=${encodeURIComponent(sigEvt.rawPayload)}`);
    }
  });

  function handleLock() {
    lock.mutate(undefined, {
      onSettled: () => {
        sessionStorage.removeItem("backend_token");
        navigate("/unlock");
      },
    });
  }

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar */}
      <aside className="flex w-56 flex-col border-r border-sidebar-border bg-sidebar">
        <div className="flex h-14 items-center gap-2 px-4">
          <Wallet className="size-5 text-sidebar-primary" />
          <span className="text-base font-semibold text-sidebar-foreground">Neo Wallet</span>
        </div>
        <Separator className="bg-sidebar-border" />
        <AccountSwitcher />
        <nav className="flex-1 space-y-1 overflow-y-auto p-2">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                  isActive
                    ? "bg-sidebar-primary/10 text-sidebar-primary"
                    : "text-muted-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
                )
              }
            >
              <item.icon className="size-4" />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <Separator className="bg-sidebar-border" />
        <div className="px-3 py-2">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <Radio className={cn("size-3", listenerConnected ? "text-green-500" : "text-muted-foreground")} />
            <span>Listener</span>
            <Badge variant={listenerConnected ? "default" : "secondary"} className="ml-auto text-[10px] px-1.5 py-0">
              {summary?.listenerStatus ?? "—"}
            </Badge>
          </div>
        </div>
        <Separator className="bg-sidebar-border" />
        <SessionExpiryBadge expiresAt={summary?.lockExpiresAt ?? null} />
        <Separator className="bg-sidebar-border" />
        <div className="p-2">
          <Button
            variant="ghost"
            className="w-full justify-start gap-3 text-muted-foreground"
            onClick={handleLock}
          >
            <LogOut className="size-4" />
            Lock Wallet
          </Button>
        </div>
        <div className="border-t border-sidebar-border p-3">
          <p className="text-xs text-muted-foreground">v0.1.0</p>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-y-auto">
        <div className="mx-auto max-w-5xl p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}

/**
 * Live session-expiry countdown shown in the sidebar.
 * Hides itself when auto-lock is disabled or the wallet is locked.
 */
function SessionExpiryBadge({ expiresAt }: { expiresAt: string | null }) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!expiresAt) return;
    const id = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(id);
  }, [expiresAt]);

  if (!expiresAt) return null;

  const target = Date.parse(expiresAt);
  if (Number.isNaN(target)) return null;

  const remainingMs = target - now;
  const remainingSec = Math.max(0, Math.floor(remainingMs / 1000));
  const mm = Math.floor(remainingSec / 60);
  const ss = remainingSec % 60;
  const text =
    remainingSec <= 0
      ? "Locking…"
      : mm >= 60
        ? `${Math.floor(mm / 60)}h ${mm % 60}m`
        : mm >= 1
          ? `${mm}m ${ss.toString().padStart(2, "0")}s`
          : `${ss}s`;

  const urgent = remainingSec > 0 && remainingSec <= 60;

  return (
    <div className="px-3 py-2">
      <div
        className={cn(
          "flex items-center gap-2 text-xs",
          urgent ? "text-amber-500" : "text-muted-foreground",
        )}
      >
        <Clock className="size-3" />
        <span>Auto-lock in</span>
        <span className="ml-auto font-mono tabular-nums">{text}</span>
      </div>
    </div>
  );
}
