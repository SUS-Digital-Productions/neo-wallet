import { useState } from "react";
import {
  Globe,
  Loader2,
  ServerOff,
  Pencil,
  Check,
  X,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { EmptyState } from "@/components/EmptyState";
import {
  useNetworks,
  useWalletSummary,
  useSetActiveNetwork,
  useSetNetworkNode,
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
  const setNode = useSetNetworkNode();
  const [switching, setSwitching] = useState<string | null>(null);
  const [editingNode, setEditingNode] = useState<string | null>(null);
  const [nodeInput, setNodeInput] = useState("");

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

  function startEditNode(chainId: string, currentNode: string) {
    setEditingNode(chainId);
    setNodeInput(currentNode);
  }

  function cancelEditNode() {
    setEditingNode(null);
    setNodeInput("");
  }

  function saveNode(chainId: string) {
    const url = nodeInput.trim();
    if (!url) return;
    setNode.mutate(
      { chainId, node: url },
      {
        onSuccess: () => {
          toast.success("Node updated");
          setEditingNode(null);
          setNodeInput("");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Failed to update node"),
      },
    );
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Networks</h1>
        <p className="text-sm text-muted-foreground">
          Switch between blockchain networks and configure RPC nodes
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Globe className="size-4 text-primary" />
            Available Networks
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
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
              const isEditingThisNode = editingNode === n.chainId;
              return (
                <div
                  key={n.chainId}
                  className="rounded-lg border px-4 py-3 space-y-2"
                >
                  <div className="flex items-center justify-between">
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

                  {/* Node row */}
                  {isEditingThisNode ? (
                    <div className="flex items-center gap-2">
                      <Input
                        className="h-7 flex-1 font-mono text-xs"
                        value={nodeInput}
                        onChange={(e) => setNodeInput(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") saveNode(n.chainId);
                          if (e.key === "Escape") cancelEditNode();
                        }}
                        autoFocus
                      />
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-7"
                        disabled={setNode.isPending}
                        onClick={() => saveNode(n.chainId)}
                      >
                        {setNode.isPending ? (
                          <Loader2 className="size-3 animate-spin" />
                        ) : (
                          <Check className="size-3 text-green-500" />
                        )}
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-7"
                        onClick={cancelEditNode}
                      >
                        <X className="size-3" />
                      </Button>
                    </div>
                  ) : (
                    <div className="flex items-center gap-2">
                      <code className="flex-1 truncate rounded bg-muted px-2 py-0.5 text-xs text-muted-foreground">
                        {n.node || "—"}
                      </code>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-7"
                        onClick={() => startEditNode(n.chainId, n.node)}
                      >
                        <Pencil className="size-3" />
                      </Button>
                    </div>
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

