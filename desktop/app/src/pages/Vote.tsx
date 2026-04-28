import { useState } from "react";
import { Vote as VoteIcon, X, Plus, Gift } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";

/**
 * Producer voting (eosio::voteproducer) and proxy registration.
 * Block producer names must be sorted alphabetically per the system contract.
 */
export default function Vote() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;

  const [mode, setMode] = useState<"producers" | "proxy" | "claim">("producers");
  const [proxy, setProxy] = useState("");
  const [producerInput, setProducerInput] = useState("");
  const [producers, setProducers] = useState<string[]>([]);

  function addProducer() {
    const p = producerInput.trim().toLowerCase();
    if (!p) return;
    if (producers.includes(p)) {
      setProducerInput("");
      return;
    }
    if (producers.length >= 30) return;
    setProducers([...producers, p]);
    setProducerInput("");
  }

  function removeProducer(p: string) {
    setProducers(producers.filter((x) => x !== p));
  }

  return (
    <Tabs value={mode} onValueChange={(v) => setMode(v as typeof mode)}>
      <TabsList className="mb-4">
        <TabsTrigger value="producers">Vote producers</TabsTrigger>
        <TabsTrigger value="proxy">Vote via proxy</TabsTrigger>
        <TabsTrigger value="claim">Claim rewards</TabsTrigger>
      </TabsList>

      <TabsContent value="producers">
        <ActionFormShell
          title="Vote for producers"
          description="Vote for up to 30 block producers (names must be alphabetically sorted)"
          icon={<VoteIcon className="size-5 text-primary" />}
          submitLabel="Cast vote"
          buildActions={() => {
            const a = requireActiveAccount(active);
            if (producers.length === 0) throw new Error("Add at least one producer");
            return [
              {
                account: "eosio",
                name: "voteproducer",
                data: {
                  voter: a.account,
                  proxy: "",
                  producers: [...producers].sort(),
                },
              },
            ];
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Producers ({producers.length}/30)</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex gap-2">
                <Input
                  placeholder="bp.account.name"
                  value={producerInput}
                  onChange={(e) => setProducerInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      addProducer();
                    }
                  }}
                />
                <Button type="button" variant="outline" onClick={addProducer}>
                  <Plus className="size-4" />
                </Button>
              </div>
              <div className="flex flex-wrap gap-1">
                {producers.map((p) => (
                  <Badge
                    key={p}
                    variant="secondary"
                    className="gap-1 font-mono text-[11px]"
                  >
                    {p}
                    <button type="button" onClick={() => removeProducer(p)}>
                      <X className="size-3" />
                    </button>
                  </Badge>
                ))}
                {producers.length === 0 && (
                  <p className="text-xs text-muted-foreground">No producers selected.</p>
                )}
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="proxy">
        <ActionFormShell
          title="Delegate to proxy"
          description="Have a proxy account vote on your behalf. Use an empty proxy to clear."
          icon={<VoteIcon className="size-5 text-primary" />}
          submitLabel="Set proxy"
          buildActions={() => {
            const a = requireActiveAccount(active);
            return [
              {
                account: "eosio",
                name: "voteproducer",
                data: {
                  voter: a.account,
                  proxy: proxy.trim(),
                  producers: [],
                },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="space-y-1">
                <Label>Proxy account</Label>
                <Input
                  placeholder="proxy.account"
                  value={proxy}
                  onChange={(e) => setProxy(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="claim">
        <ActionFormShell
          title="Claim rewards"
          description="Producers and voters can claim rewards via eosio::claimrewards"
          icon={<Gift className="size-5 text-primary" />}
          submitLabel="Claim"
          buildActions={() => {
            const a = requireActiveAccount(active);
            return [
              {
                account: "eosio",
                name: "claimrewards",
                data: { owner: a.account },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="py-4 text-sm text-muted-foreground">
              Calls <code className="font-mono">eosio::claimrewards</code> for{" "}
              <span className="font-mono">{active?.account ?? "<no account>"}</span>.
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>
    </Tabs>
  );
}
