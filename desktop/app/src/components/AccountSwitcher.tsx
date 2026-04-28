import { useState, useRef, useEffect } from "react";
import { Check, ChevronDown, Wallet, Plus } from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { Separator } from "@/components/ui/separator";
import {
  useAccounts,
  useNetworks,
  useWalletSummary,
  useSetActiveAccount,
  useSetActiveNetwork,
} from "@/api/hooks";

/**
 * Compact sidebar header that shows the active account & network and lets
 * the user switch with a single click. Replaces the picker UI on Dashboard.
 */
export function AccountSwitcher() {
  const { data: summary } = useWalletSummary({ refetchInterval: 10_000 });
  const { data: accounts = [] } = useAccounts();
  const { data: networks = [] } = useNetworks();
  const setAccount = useSetActiveAccount();
  const setNetwork = useSetActiveNetwork();
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement | null>(null);

  // Close when clicking outside
  useEffect(() => {
    if (!open) return;
    function onDoc(e: MouseEvent) {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);

  const active = summary?.activeAccount ?? null;
  const activeChainId = summary?.activeNetwork?.chainId;
  // Group accounts by chain for clearer multi-chain view
  const grouped = accounts.reduce<Record<string, typeof accounts>>((acc, a) => {
    (acc[a.chainId] ??= []).push(a);
    return acc;
  }, {});

  function chooseAccount(a: { account: string; authority: string; chainId: string }) {
    setAccount.mutate(
      { account: a.account, authority: a.authority, chainId: a.chainId },
      {
        onSuccess: () => {
          toast.success(`Switched to ${a.account}@${a.authority}`);
          setOpen(false);
        },
        onError: (err) => toast.error(err instanceof Error ? err.message : "Failed to switch"),
      },
    );
  }

  function chooseNetwork(chainId: string) {
    setNetwork.mutate(chainId, {
      onSuccess: () => toast.success("Network switched"),
      onError: (err) => toast.error(err instanceof Error ? err.message : "Failed"),
    });
  }

  return (
    <div ref={wrapRef} className="relative px-2 pt-2">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className={cn(
          "flex w-full items-center gap-2 rounded-md border border-sidebar-border/60 bg-sidebar-accent/40 px-2.5 py-2 text-left text-xs hover:bg-sidebar-accent",
        )}
      >
        <div className="flex size-7 shrink-0 items-center justify-center rounded-md bg-sidebar-primary/15 text-sidebar-primary">
          <Wallet className="size-3.5" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-medium text-sidebar-foreground">
            {active?.account ?? "No account"}
          </div>
          <div className="truncate text-[10px] text-muted-foreground">
            {active ? `@${active.authority} · ${active.chainName}` : "Click to import"}
          </div>
        </div>
        <ChevronDown
          className={cn(
            "size-3.5 shrink-0 text-muted-foreground transition-transform",
            open && "rotate-180",
          )}
        />
      </button>

      {open && (
        <div className="absolute inset-x-2 top-full z-50 mt-1 max-h-[60vh] overflow-y-auto rounded-md border border-sidebar-border bg-popover p-1 shadow-lg">
          {/* Network section */}
          {networks.length > 1 && (
            <>
              <div className="px-2 py-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                Network
              </div>
              {networks.map((n) => (
                <button
                  key={n.chainId}
                  type="button"
                  onClick={() => chooseNetwork(n.chainId)}
                  className={cn(
                    "flex w-full items-center justify-between rounded px-2 py-1.5 text-left text-xs transition-colors",
                    activeChainId === n.chainId
                      ? "bg-primary/10 text-primary"
                      : "hover:bg-accent",
                  )}
                >
                  <span>
                    {n.name} <span className="text-muted-foreground">({n.symbol})</span>
                  </span>
                  {activeChainId === n.chainId && <Check className="size-3.5" />}
                </button>
              ))}
              <Separator className="my-1" />
            </>
          )}

          {/* Accounts grouped by chain */}
          {accounts.length === 0 ? (
            <div className="px-2 py-3 text-xs text-muted-foreground">
              No accounts imported.
            </div>
          ) : (
            Object.entries(grouped).map(([chainId, list]) => {
              const chain = networks.find((n) => n.chainId === chainId);
              return (
                <div key={chainId}>
                  <div className="px-2 py-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                    {chain?.name ?? chainId.slice(0, 12)}
                  </div>
                  {list.map((a) => {
                    const isActive =
                      active?.account === a.account &&
                      active?.authority === a.authority &&
                      active?.chainId === a.chainId;
                    return (
                      <button
                        key={`${a.account}@${a.authority}@${a.chainId}`}
                        type="button"
                        disabled={setAccount.isPending}
                        onClick={() => chooseAccount(a)}
                        className={cn(
                          "flex w-full items-center justify-between rounded px-2 py-1.5 text-left text-xs transition-colors",
                          isActive ? "bg-primary/10 text-primary" : "hover:bg-accent",
                        )}
                      >
                        <span className="truncate">
                          <span className="font-medium">{a.account}</span>
                          <span className="ml-1 text-muted-foreground">@{a.authority}</span>
                        </span>
                        {isActive && <Check className="size-3.5 shrink-0" />}
                      </button>
                    );
                  })}
                </div>
              );
            })
          )}

          <Separator className="my-1" />
          <Link
            to="/keys"
            onClick={() => setOpen(false)}
            className="flex items-center gap-2 rounded px-2 py-1.5 text-xs text-muted-foreground hover:bg-accent hover:text-foreground"
          >
            <Plus className="size-3.5" />
            Import account
          </Link>
        </div>
      )}
    </div>
  );
}
