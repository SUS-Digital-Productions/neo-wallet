import { useState } from "react";
import { ScrollText, AlertCircle } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";
import type { SignAction } from "@/api/types";

/**
 * eosio.msig multi-signature workflow:
 *   - propose:    submit a transaction proposal
 *   - approve:    sign-off on a proposal
 *   - unapprove:  revoke a previous approval
 *   - cancel:     proposer cancels the proposal
 *   - exec:       execute once thresholds are met
 *
 * The propose form takes the inner-transaction body as raw JSON
 * (so users can paste a transaction prepared elsewhere).
 */
export default function Msig() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;

  const [tab, setTab] = useState<"propose" | "approve" | "unapprove" | "cancel" | "exec">(
    "propose",
  );

  // Common
  const [proposer, setProposer] = useState("");
  const [proposalName, setProposalName] = useState("");

  // Propose
  const [requested, setRequested] = useState(
    JSON.stringify([{ actor: "alice", permission: "active" }], null, 2),
  );
  const [trxJson, setTrxJson] = useState(
    JSON.stringify(
      {
        expiration: "2030-01-01T00:00:00",
        ref_block_num: 0,
        ref_block_prefix: 0,
        max_net_usage_words: 0,
        max_cpu_usage_ms: 0,
        delay_sec: 0,
        context_free_actions: [],
        actions: [
          {
            account: "eosio.token",
            name: "transfer",
            authorization: [{ actor: "alice", permission: "active" }],
            data: { from: "alice", to: "bob", quantity: "1.0000 EOS", memo: "" },
          },
        ],
        transaction_extensions: [],
      },
      null,
      2,
    ),
  );

  // Approve / Unapprove / Cancel / Exec
  const [level, setLevel] = useState(""); // actor@permission
  const [executer, setExecuter] = useState("");
  const [proposalHash, setProposalHash] = useState("");

  function parseLevel(s: string) {
    const [actor, permission] = s.split("@");
    if (!actor) throw new Error(`Invalid permission level: ${s}`);
    return { actor: actor.trim(), permission: (permission ?? "active").trim() };
  }

  return (
    <Tabs value={tab} onValueChange={(v) => setTab(v as typeof tab)}>
      <TabsList className="mb-4">
        <TabsTrigger value="propose">Propose</TabsTrigger>
        <TabsTrigger value="approve">Approve</TabsTrigger>
        <TabsTrigger value="unapprove">Unapprove</TabsTrigger>
        <TabsTrigger value="cancel">Cancel</TabsTrigger>
        <TabsTrigger value="exec">Exec</TabsTrigger>
      </TabsList>

      <TabsContent value="propose">
        <ActionFormShell
          title="MSIG · Propose"
          description="Submit a multi-signature proposal to eosio.msig"
          icon={<ScrollText className="size-5 text-primary" />}
          submitLabel="Propose"
          buildActions={(): SignAction[] => {
            const a = requireActiveAccount(active);
            if (!proposalName.trim()) throw new Error("Proposal name required");
            let trx: unknown;
            let req: unknown;
            try {
              trx = JSON.parse(trxJson);
            } catch (e) {
              throw new Error(`Invalid transaction JSON: ${(e as Error).message}`);
            }
            try {
              req = JSON.parse(requested);
            } catch (e) {
              throw new Error(`Invalid requested JSON: ${(e as Error).message}`);
            }
            return [
              {
                account: "eosio.msig",
                name: "propose",
                data: {
                  proposer: proposer.trim() || a.account,
                  proposal_name: proposalName.trim(),
                  requested: req,
                  trx,
                },
              },
            ];
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Proposal</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Proposer (default: you)</Label>
                  <Input
                    value={proposer}
                    onChange={(e) => setProposer(e.target.value)}
                    placeholder={active?.account ?? ""}
                  />
                </div>
                <div className="space-y-1">
                  <Label>Proposal name</Label>
                  <Input
                    value={proposalName}
                    onChange={(e) => setProposalName(e.target.value)}
                    placeholder="myproposal1"
                  />
                </div>
              </div>
              <div className="space-y-1">
                <Label>Requested approvals (JSON)</Label>
                <Textarea
                  rows={4}
                  className="font-mono text-xs"
                  value={requested}
                  onChange={(e) => setRequested(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label>Transaction body (JSON)</Label>
                <Textarea
                  rows={12}
                  className="font-mono text-xs"
                  value={trxJson}
                  onChange={(e) => setTrxJson(e.target.value)}
                />
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="flex gap-2 py-3 text-xs text-muted-foreground">
              <AlertCircle className="size-4 shrink-0" />
              <p>
                The transaction's <code className="font-mono">expiration</code> and TAPOS
                fields must be set ahead of time. Use Utilities to inspect chain info.
              </p>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      {(["approve", "unapprove"] as const).map((action) => (
        <TabsContent key={action} value={action}>
          <ActionFormShell
            title={`MSIG · ${action === "approve" ? "Approve" : "Unapprove"}`}
            description={`${action === "approve" ? "Approve" : "Revoke approval of"} an existing proposal`}
            icon={<ScrollText className="size-5 text-primary" />}
            submitLabel={action === "approve" ? "Approve" : "Unapprove"}
            buildActions={(): SignAction[] => {
              const a = requireActiveAccount(active);
              if (!proposer.trim() || !proposalName.trim() || !level.trim())
                throw new Error("Proposer, proposal name and level required");
              const data: Record<string, unknown> = {
                proposer: proposer.trim(),
                proposal_name: proposalName.trim(),
                level: parseLevel(level || `${a.account}@${a.authority}`),
              };
              if (action === "approve" && proposalHash.trim()) {
                data.proposal_hash = proposalHash.trim();
              }
              return [{ account: "eosio.msig", name: action, data }];
            }}
          >
            <Card>
              <CardContent className="space-y-3 pt-6">
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1">
                    <Label>Proposer</Label>
                    <Input value={proposer} onChange={(e) => setProposer(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label>Proposal name</Label>
                    <Input
                      value={proposalName}
                      onChange={(e) => setProposalName(e.target.value)}
                    />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label>Level (actor@permission)</Label>
                  <Input
                    value={level}
                    onChange={(e) => setLevel(e.target.value)}
                    placeholder={
                      active ? `${active.account}@${active.authority}` : "actor@active"
                    }
                  />
                </div>
                {action === "approve" && (
                  <div className="space-y-1">
                    <Label>Proposal hash (optional)</Label>
                    <Input
                      className="font-mono text-xs"
                      value={proposalHash}
                      onChange={(e) => setProposalHash(e.target.value)}
                    />
                  </div>
                )}
              </CardContent>
            </Card>
          </ActionFormShell>
        </TabsContent>
      ))}

      <TabsContent value="cancel">
        <ActionFormShell
          title="MSIG · Cancel"
          description="Proposer cancels their own pending proposal"
          icon={<ScrollText className="size-5 text-primary" />}
          submitLabel="Cancel proposal"
          buildActions={(): SignAction[] => {
            const a = requireActiveAccount(active);
            if (!proposer.trim() || !proposalName.trim())
              throw new Error("Proposer and proposal name required");
            return [
              {
                account: "eosio.msig",
                name: "cancel",
                data: {
                  proposer: proposer.trim(),
                  proposal_name: proposalName.trim(),
                  canceler: a.account,
                },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Proposer</Label>
                  <Input value={proposer} onChange={(e) => setProposer(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label>Proposal name</Label>
                  <Input
                    value={proposalName}
                    onChange={(e) => setProposalName(e.target.value)}
                  />
                </div>
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>

      <TabsContent value="exec">
        <ActionFormShell
          title="MSIG · Exec"
          description="Execute a proposal that has reached its approval threshold"
          icon={<ScrollText className="size-5 text-primary" />}
          submitLabel="Execute"
          buildActions={(): SignAction[] => {
            const a = requireActiveAccount(active);
            if (!proposer.trim() || !proposalName.trim())
              throw new Error("Proposer and proposal name required");
            return [
              {
                account: "eosio.msig",
                name: "exec",
                data: {
                  proposer: proposer.trim(),
                  proposal_name: proposalName.trim(),
                  executer: executer.trim() || a.account,
                },
              },
            ];
          }}
        >
          <Card>
            <CardContent className="space-y-3 pt-6">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label>Proposer</Label>
                  <Input value={proposer} onChange={(e) => setProposer(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label>Proposal name</Label>
                  <Input
                    value={proposalName}
                    onChange={(e) => setProposalName(e.target.value)}
                  />
                </div>
              </div>
              <div className="space-y-1">
                <Label>Executer (default: you)</Label>
                <Input value={executer} onChange={(e) => setExecuter(e.target.value)} />
              </div>
            </CardContent>
          </Card>
        </ActionFormShell>
      </TabsContent>
    </Tabs>
  );
}
