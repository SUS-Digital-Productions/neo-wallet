import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
  FileCheck,
  Loader2,
  ShieldAlert,
  X,
  Code,
  Link as LinkIcon,
} from "lucide-react";
import { QrScanner } from "@/components/QrScanner";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  AccountSearchSelect,
  accountSelectValue,
} from "@/components/AccountSearchSelect";
import type { AccountInfo, EsrParseResponse, SignRawAction } from "@/api/types";
import {
  useParseEsr,
  useApproveEsr,
  useRejectEsr,
  useSignRawTransaction,
  useNetworks,
  useWalletSummary,
  useAccounts,
} from "@/api/hooks";
import { getPendingEsr } from "@/api/client";

const EXAMPLE_ACTIONS = `[
  {
    "account": "eosio.token",
    "name": "transfer",
    "data": {
      "from": "myaccount",
      "to": "receiver",
      "quantity": "1.00000000 WAX",
      "memo": "hello"
    }
  }
]`;

function findAccountByValue(accounts: AccountInfo[], value: string) {
  return accounts.find((account) => accountSelectValue(account) === value) ?? null;
}

export default function EsrApproval() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // ESR URI tab
  const [uri, setUri] = useState(searchParams.get("uri") ?? "");
  const [parsed, setParsed] = useState<EsrParseResponse | null>(null);
  const parseEsr = useParseEsr();
  const approveEsr = useApproveEsr();
  const rejectEsr = useRejectEsr();

  // Raw payload tab
  const [actionsJson, setActionsJson] = useState("");
  const [broadcast, setBroadcast] = useState(true);
  const [selectedEsrAccountKey, setSelectedEsrAccountKey] = useState("");
  const [selectedRawAccountKey, setSelectedRawAccountKey] = useState("");
  const signRaw = useSignRawTransaction();
  const { data: networks = [] } = useNetworks();
  const { data: summary } = useWalletSummary();
  const { data: accounts = [], isLoading: accountsLoading } = useAccounts();

  function doParse(esrUri: string) {
    if (!esrUri.trim()) return;
    setParsed(null);
    parseEsr.mutate(
      { uri: esrUri.trim() },
      {
        onSuccess: (res) => setParsed(res),
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Failed to parse ESR"),
      },
    );
  }

  function handleParse(e: React.FormEvent) {
    e.preventDefault();
    doParse(uri);
  }

  const handleQrScan = useCallback(
    (data: string) => {
      setUri(data);
      doParse(data);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );

  function handleApprove() {
    if (!parsed) return;
    const selectedAccount = findAccountByValue(accounts, selectedEsrAccountKey);
    if (!selectedAccount) {
      toast.error("Select a signing account");
      return;
    }

    const isIdentity = parsed.type === "identity";
    approveEsr.mutate(
      {
        requestId: parsed.requestId,
        broadcast: false,
        account: selectedAccount.account,
        authority: selectedAccount.authority,
        chainId: selectedAccount.chainId,
      },
      {
        onSuccess: (res) => {
          toast.success(
            isIdentity
              ? "Identity approved"
              : `Signed – tx ${res.transactionId.slice(0, 12)}…`,
          );
          navigate("/");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Approval failed"),
      },
    );
  }

  function handleReject() {
    if (!parsed) return;
    rejectEsr.mutate(
      { requestId: parsed.requestId, reason: "User rejected" },
      {
        onSuccess: () => {
          toast.info("Request rejected");
          navigate("/");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Rejection failed"),
      },
    );
  }

  function handleSignRaw(e: React.FormEvent) {
    e.preventDefault();
    const selectedAccount = findAccountByValue(accounts, selectedRawAccountKey);
    if (!actionsJson.trim() || !selectedAccount) return;

    let actions: SignRawAction[];
    try {
      actions = JSON.parse(actionsJson);
      if (!Array.isArray(actions)) throw new Error("Actions must be an array");
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Invalid JSON – expected an array of actions",
      );
      return;
    }

    signRaw.mutate(
      {
        chainId: selectedAccount.chainId,
        actions,
        broadcast,
        account: selectedAccount.account,
        authority: selectedAccount.authority,
      },
      {
        onSuccess: (res) => {
          toast.success(`Signed – tx ${res.transactionId.slice(0, 12)}…`);
          navigate("/");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Sign failed"),
      },
    );
  }

  // Auto-load when navigated with ?requestId= (from relay) or ?uri= (from deep link / manual)
  useEffect(() => {
    const paramRequestId = searchParams.get("requestId");
    const paramUri = searchParams.get("uri");

    if (paramRequestId && !parsed) {
      // Fetch pre-parsed request from backend (relay-originated)
      getPendingEsr(paramRequestId)
        .then((res) => setParsed(res))
        .catch((err) =>
          toast.error(err instanceof Error ? err.message : "Failed to load request"),
        );
    } else if (paramUri && !parsed && !parseEsr.isPending) {
      const decoded = decodeURIComponent(paramUri);
      setUri(decoded);
      parseEsr.mutate(
        { uri: decoded },
        {
          onSuccess: (res) => setParsed(res),
          onError: (err) =>
            toast.error(err instanceof Error ? err.message : "Failed to parse ESR"),
        },
      );
    }
    // Only run on mount / when searchParams change
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const esrAccounts = useMemo(() => {
    if (!parsed?.chainId) return accounts;
    return accounts.filter((account) => account.chainId === parsed.chainId);
  }, [accounts, parsed?.chainId]);
  const selectedEsrAccount = findAccountByValue(esrAccounts, selectedEsrAccountKey);
  const selectedRawAccount = findAccountByValue(accounts, selectedRawAccountKey);
  const rawChainId = selectedRawAccount?.chainId ?? summary?.activeNetwork?.chainId ?? "";
  const activeNet = networks.find((n) => n.chainId === rawChainId);
  const esrSubmitting = approveEsr.isPending || rejectEsr.isPending;

  useEffect(() => {
    if (!parsed) {
      setSelectedEsrAccountKey("");
      return;
    }

    if (esrAccounts.length === 0) {
      setSelectedEsrAccountKey("");
      return;
    }

    if (findAccountByValue(esrAccounts, selectedEsrAccountKey)) return;

    const activeKey = summary?.activeAccount
      ? accountSelectValue(summary.activeAccount)
      : "";
    const nextAccount =
      esrAccounts.find((account) => accountSelectValue(account) === activeKey) ??
      esrAccounts[0];
    setSelectedEsrAccountKey(accountSelectValue(nextAccount));
  }, [esrAccounts, parsed, selectedEsrAccountKey, summary?.activeAccount]);

  useEffect(() => {
    if (accounts.length === 0) {
      setSelectedRawAccountKey("");
      return;
    }

    if (findAccountByValue(accounts, selectedRawAccountKey)) return;

    const activeKey = summary?.activeAccount
      ? accountSelectValue(summary.activeAccount)
      : "";
    const nextAccount =
      accounts.find((account) => accountSelectValue(account) === activeKey) ??
      accounts[0];
    setSelectedRawAccountKey(accountSelectValue(nextAccount));
  }, [accounts, selectedRawAccountKey, summary?.activeAccount]);

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Signing Request</h1>
        <p className="text-sm text-muted-foreground">
          Sign an ESR URI or build a raw transaction payload
        </p>
      </div>

      <Tabs defaultValue="esr" className="w-full">
        <TabsList className="w-full">
          <TabsTrigger value="esr" className="flex-1 gap-1.5">
            <LinkIcon className="size-3.5" />
            ESR URI
          </TabsTrigger>
          <TabsTrigger value="raw" className="flex-1 gap-1.5">
            <Code className="size-3.5" />
            Raw Payload
          </TabsTrigger>
        </TabsList>

        {/* ─── ESR URI Tab ─── */}
        <TabsContent value="esr" className="space-y-4 pt-2">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <FileCheck className="size-4 text-primary" />
                ESR URI
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleParse} className="space-y-3">
                <div className="space-y-2">
                  <Label htmlFor="esr-uri">Signing Request URI</Label>
                  <div className="flex gap-2">
                    <Input
                      id="esr-uri"
                      placeholder="esr://..."
                      value={uri}
                      onChange={(e) => setUri(e.target.value)}
                      required
                      className="flex-1"
                    />
                    <QrScanner onScan={handleQrScan} />
                  </div>
                </div>
                <Button
                  type="submit"
                  variant="outline"
                  disabled={parseEsr.isPending}
                >
                  {parseEsr.isPending && (
                    <Loader2 className="size-4 animate-spin" />
                  )}
                  Parse
                </Button>
              </form>
            </CardContent>
          </Card>

          {parseEsr.isPending && (
            <div className="space-y-2">
              <Skeleton className="h-32 w-full rounded-lg" />
            </div>
          )}

          {parsed && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <ShieldAlert className="size-4 text-destructive" />
                  {parsed.type === "identity" ? "Review Identity" : "Review Actions"}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Type</span>
                  <Badge variant="secondary">{parsed.type}</Badge>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Chain</span>
                  <span className="font-mono text-xs">
                    {parsed.chainId.slice(0, 16)}…
                  </span>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="esr-signing-account">Signing Account</Label>
                  <AccountSearchSelect
                    id="esr-signing-account"
                    accounts={esrAccounts}
                    value={selectedEsrAccountKey}
                    onValueChange={setSelectedEsrAccountKey}
                    disabled={accountsLoading || esrSubmitting}
                    placeholder="Select signing account"
                    emptyText="No matching accounts"
                  />
                  {parsed.chainId && esrAccounts.length === 0 && (
                    <p className="text-xs text-muted-foreground">
                      No imported accounts on this request's chain.
                    </p>
                  )}
                </div>

                <Separator />

                {parsed.type !== "identity" && (
                  <div className="space-y-2">
                    <p className="text-sm font-medium">Actions</p>
                    {parsed.actions.map((a, i) => (
                      <div
                        key={i}
                        className="rounded-lg border px-3 py-2 text-sm font-mono"
                      >
                        {a.account}::{a.name}
                      </div>
                    ))}
                  </div>
                )}

                <Separator />

                <div className="flex gap-3">
                  <Button
                    onClick={handleApprove}
                    className="flex-1"
                    disabled={esrSubmitting || !selectedEsrAccount}
                  >
                    {approveEsr.isPending && (
                      <Loader2 className="size-4 animate-spin" />
                    )}
                    {parsed.type === "identity" ? "Approve Identity" : "Sign & Return"}
                  </Button>
                  <Button
                    variant="outline"
                    onClick={handleReject}
                    disabled={esrSubmitting}
                  >
                    <X className="size-4" />
                    Reject
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* ─── Raw Payload Tab ─── */}
        <TabsContent value="raw" className="space-y-4 pt-2">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Code className="size-4 text-primary" />
                Raw Transaction
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSignRaw} className="space-y-4">
                {/* Active chain info */}
                <div className="space-y-2">
                  <Label htmlFor="raw-signing-account">Signing Account</Label>
                  <AccountSearchSelect
                    id="raw-signing-account"
                    accounts={accounts}
                    value={selectedRawAccountKey}
                    onValueChange={setSelectedRawAccountKey}
                    disabled={accountsLoading || signRaw.isPending}
                    placeholder="Select signing account"
                    emptyText="No accounts found"
                  />
                </div>

                {activeNet && (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <span>Signing on</span>
                    <Badge variant="secondary">{activeNet.name}</Badge>
                    <span className="font-mono text-xs">
                      ({rawChainId.slice(0, 12)}…)
                    </span>
                  </div>
                )}

                <div className="space-y-2">
                  <Label htmlFor="actions-json">Actions (JSON array)</Label>
                  <Textarea
                    id="actions-json"
                    placeholder={EXAMPLE_ACTIONS}
                    value={actionsJson}
                    onChange={(e) => setActionsJson(e.target.value)}
                    rows={12}
                    className="font-mono text-xs"
                    required
                  />
                  <p className="text-xs text-muted-foreground">
                    Each action needs{" "}
                    <code className="rounded bg-muted px-1">account</code>,{" "}
                    <code className="rounded bg-muted px-1">name</code>, and{" "}
                    <code className="rounded bg-muted px-1">data</code> fields
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    id="broadcast"
                    checked={broadcast}
                    onChange={(e) => setBroadcast(e.target.checked)}
                    className="rounded border-muted-foreground"
                  />
                  <Label htmlFor="broadcast" className="text-sm">
                    Broadcast transaction
                  </Label>
                </div>

                <Button
                  type="submit"
                  className="w-full"
                  disabled={signRaw.isPending || !selectedRawAccount}
                >
                  {signRaw.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <FileCheck className="size-4" />
                  )}
                  Sign {broadcast ? "& Broadcast" : "Transaction"}
                </Button>
              </form>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
