import { useState } from "react";
import {
  Globe,
  Loader2,
  ServerOff,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/EmptyState";
import {
  useNetworks,
  useWalletSummary,
  useSetActiveNetwork,
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

export default function Networks() {
  const { data: networks = [], isLoading } = useNetworks();
  const { data: summary } = useWalletSummary();
  const setNetwork = useSetActiveNetwork();
  const [switching, setSwitching] = useState<string | null>(null);

  const activeNet = summary?.activeNetwork?.chainId ?? null;

  function switchNetwork(chainId: string) {
    setSwitching(chainId);
    setNetwork.mutate(chainId, {
      onSuccess: () => toast.success("Network changed"),
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : "Failed to switch"),
      onSettled: () => setSwitching(null),
    });
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Networks</h1>
        <p className="text-sm text-muted-foreground">
          Switch between blockchain networks
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Globe className="size-4 text-primary" />
            Available Networks
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {isLoading ? (
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
              const isSwitching = switching === n.chainId;
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
    </div>
  );
}
