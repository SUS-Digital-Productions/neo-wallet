import { useState } from "react";
import {
  KeyRound,
  Loader2,
  Plus,
  Search,
  Check,
  Globe,
  SearchX,
  Trash2,
  X,
  Import as ImportIcon,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/EmptyState";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import type {
  LookupAccountsResponse,
  ImportAccountEntry,
  AccountInfo,
  KeyInfo,
} from "@/api/types";
import {
  useKeys,
  useAddKey,
  useRemoveKey,
  useLookupAccounts,
  useLookupStoredKeyAccounts,
  useImportAccounts,
  useImportStoredKeyAccounts,
  useAccounts,
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

type LookupSource =
  | { type: "stored"; publicKey: string }
  | { type: "manual"; privateKey: string };

export default function Keys() {
  const { data: keys = [], isLoading: keysLoading } = useKeys();
  const { data: existingAccounts = [] } = useAccounts();
  const addKeyMutation = useAddKey();
  const removeKeyMutation = useRemoveKey();
  const manualLookup = useLookupAccounts();
  const storedLookup = useLookupStoredKeyAccounts();
  const manualImportMutation = useImportAccounts();
  const storedImportMutation = useImportStoredKeyAccounts();

  // Add key form
  const [privateKey, setPrivateKey] = useState("");
  const [label, setLabel] = useState("");
  const [removeTarget, setRemoveTarget] = useState<KeyInfo | null>(null);

  // Lookup state
  const [keyFilter, setKeyFilter] = useState("");
  const [manualPrivateKey, setManualPrivateKey] = useState("");
  const [lookupSource, setLookupSource] = useState<LookupSource | null>(null);
  const [lookupResult, setLookupResult] = useState<LookupAccountsResponse | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  function entryKey(chainId: string, account: string, authority: string) {
    return `${chainId}::${account}::${authority}`;
  }

  function isAlreadyImported(chainId: string, account: string, authority: string) {
    return existingAccounts.some(
      (a) => a.account === account && a.authority === authority && a.chainId === chainId,
    );
  }

  function toggleSelection(key: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function handleAddKey(e: React.FormEvent) {
    e.preventDefault();
    if (!privateKey.trim()) return;

    addKeyMutation.mutate(
      { privateKey: privateKey.trim(), label: label.trim() },
      {
        onSuccess: () => {
          toast.success("Key added");
          setPrivateKey("");
          setLabel("");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Failed to add key"),
      },
    );
  }

  function handleLookupSuccess(result: LookupAccountsResponse) {
    setLookupResult(result);
    const all = new Set<string>();
    for (const chain of result.chains) {
      for (const acct of chain.accounts) {
        if (!isAlreadyImported(chain.chainId, acct.account, acct.authority)) {
          all.add(entryKey(chain.chainId, acct.account, acct.authority));
        }
      }
    }
    setSelected(all);

    const totalAccounts = result.chains.reduce(
      (sum, c) => sum + c.accounts.length,
      0,
    );
    if (totalAccounts === 0) {
      toast.info("No accounts found for this key on any chain");
    } else {
      toast.success(`Found ${totalAccounts} account(s) across chains`);
    }
  }

  function startLookup(source: LookupSource) {
    setLookupSource(source);
    setLookupResult(null);
    setSelected(new Set());
  }

  function handleStoredLookup(publicKey: string) {
    const source: LookupSource = { type: "stored", publicKey };
    startLookup(source);

    storedLookup.mutate(
      { publicKey },
      {
        onSuccess: handleLookupSuccess,
        onError: (err) => {
          clearLookup();
          toast.error(err instanceof Error ? err.message : "Lookup failed");
        },
      },
    );
  }

  function handleManualLookup(e: React.FormEvent) {
    e.preventDefault();
    const wif = manualPrivateKey.trim();
    if (!wif) return;

    const source: LookupSource = { type: "manual", privateKey: wif };
    startLookup(source);

    manualLookup.mutate(
      { privateKey: wif },
      {
        onSuccess: handleLookupSuccess,
        onError: (err) => {
          clearLookup();
          toast.error(err instanceof Error ? err.message : "Lookup failed");
        },
      },
    );
  }

  function handleImport() {
    if (!lookupResult || selected.size === 0 || !lookupSource) return;

    const accounts: ImportAccountEntry[] = [];
    for (const key of selected) {
      const [chainId, account, authority] = key.split("::");
      accounts.push({ chainId, account, authority });
    }

    const resetAfterImport = (imported: AccountInfo[]) => {
      toast.success(`Imported ${imported.length} account(s)`);
      setLookupResult(null);
      setSelected(new Set());
      setLookupSource(null);
      if (lookupSource.type === "manual") setManualPrivateKey("");
    };

    const options = {
      onSuccess: resetAfterImport,
      onError: (err: unknown) =>
        toast.error(err instanceof Error ? err.message : "Import failed"),
    };

    if (lookupSource.type === "stored") {
      storedImportMutation.mutate(
        { publicKey: lookupSource.publicKey, accounts },
        options,
      );
    } else {
      manualImportMutation.mutate(
        { privateKey: lookupSource.privateKey, accounts },
        options,
      );
    }
  }

  function clearLookup() {
    setLookupResult(null);
    setSelected(new Set());
    setLookupSource(null);
  }

  function renderLookupResultsPanel() {
    if (!lookupResult) return null;

    return (
      <div className="rounded-lg border bg-background p-3">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-sm font-medium">
              <Globe className="size-3.5 text-primary" />
              Found Accounts
              {totalFound > 0 && (
                <span className="text-xs font-normal text-muted-foreground">
                  {totalFound} found
                </span>
              )}
            </div>
            <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
              {lookupResult.publicKey}
            </p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {totalFound > 0 && selected.size > 0 && (
              <Button size="sm" disabled={importPending} onClick={handleImport}>
                {importPending ? (
                  <Loader2 className="size-3.5 animate-spin" />
                ) : (
                  <ImportIcon className="size-3.5" />
                )}
                Import {selected.size}
              </Button>
            )}
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={clearLookup}
              title="Close results"
              aria-label="Close results"
            >
              <X className="size-3.5" />
            </Button>
          </div>
        </div>

        {totalFound === 0 ? (
          <div className="pt-3">
            <EmptyState
              icon={SearchX}
              title="No accounts found"
              description="This key has no linked accounts on any supported chain"
            />
          </div>
        ) : (
          <div className="mt-3 max-h-80 space-y-3 overflow-y-auto pr-1">
            {lookupResult.chains.map((chain) => (
              <div key={chain.chainId}>
                <div className="mb-2 flex items-center gap-2">
                  {chainBadge(chain.symbol)}
                  <span className="text-sm font-medium">{chain.name}</span>
                  <span className="text-xs text-muted-foreground">
                    ({chain.accounts.length} found)
                  </span>
                </div>

                {chain.accounts.length === 0 ? (
                  <p className="ml-1 text-xs text-muted-foreground">
                    No accounts on this chain
                  </p>
                ) : (
                  <div className="space-y-1">
                    {chain.accounts.map((acct) => {
                      const key = entryKey(
                        chain.chainId,
                        acct.account,
                        acct.authority,
                      );
                      const alreadyImported = isAlreadyImported(
                        chain.chainId,
                        acct.account,
                        acct.authority,
                      );
                      const isSelected = selected.has(key);
                      return (
                        <button
                          key={key}
                          type="button"
                          disabled={alreadyImported}
                          onClick={() => toggleSelection(key)}
                          className={`flex w-full items-center justify-between rounded-md border px-3 py-2 text-left transition-colors ${
                            alreadyImported
                              ? "cursor-not-allowed border-border bg-muted/50 opacity-60"
                              : isSelected
                                ? "border-primary/50 bg-primary/5"
                                : "border-border hover:bg-accent"
                          }`}
                        >
                          <div>
                            <p className="text-sm font-medium">
                              {acct.account}
                              <span className="ml-1 text-muted-foreground">
                                @{acct.authority}
                              </span>
                            </p>
                          </div>
                          {alreadyImported ? (
                            <span className="text-xs text-muted-foreground">Imported</span>
                          ) : isSelected ? (
                            <Check className="size-4 text-primary" />
                          ) : null}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        {totalFound > 0 && selected.size === 0 && (
          <p className="pt-3 text-center text-xs text-muted-foreground">
            All found accounts are already imported.
          </p>
        )}
      </div>
    );
  }

  const totalFound =
    lookupResult?.chains.reduce((sum, c) => sum + c.accounts.length, 0) ?? 0;
  const lookupPending = manualLookup.isPending || storedLookup.isPending;
  const importPending = manualImportMutation.isPending || storedImportMutation.isPending;
  const normalizedKeyFilter = keyFilter.trim().toLowerCase();
  const filteredKeys = normalizedKeyFilter
    ? keys.filter((k) =>
        k.publicKey.toLowerCase().includes(normalizedKeyFilter) ||
        (k.label ?? "").toLowerCase().includes(normalizedKeyFilter),
      )
    : keys;

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Key Management</h1>
        <p className="text-sm text-muted-foreground">
          Store private keys and link them to blockchain accounts
        </p>
      </div>

      {/* Add Key */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Plus className="size-4 text-primary" />
            Add Private Key
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleAddKey} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="pk">Private Key (WIF)</Label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="pk"
                  type="password"
                  placeholder="5K… or PVT_K1_…"
                  value={privateKey}
                  onChange={(e) => setPrivateKey(e.target.value)}
                  className="pl-9"
                  required
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="label">Label (optional)</Label>
              <Input
                id="label"
                placeholder="e.g. Main key, Trading key"
                value={label}
                onChange={(e) => setLabel(e.target.value)}
              />
            </div>
            <Button
              type="submit"
              className="w-full"
              disabled={addKeyMutation.isPending || !privateKey.trim()}
            >
              {addKeyMutation.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Plus className="size-4" />
              )}
              Add Key
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Stored Keys */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <KeyRound className="size-4 text-primary" />
            Stored Keys
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {keys.length > 4 && (
            <div className="relative">
              <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={keyFilter}
                onChange={(e) => setKeyFilter(e.target.value)}
                placeholder="Filter keys by public key or label"
                className="pl-9"
              />
            </div>
          )}

          {keysLoading ? (
            <div className="space-y-2">
              {[1, 2].map((i) => (
                <Skeleton key={i} className="h-14 w-full rounded-lg" />
              ))}
            </div>
          ) : keys.length === 0 ? (
            <EmptyState
              icon={KeyRound}
              title="No keys stored"
              description="Add a private key above to get started"
            />
          ) : filteredKeys.length === 0 ? (
            <EmptyState
              icon={SearchX}
              title="No matching keys"
              description="Try a different public key or label"
            />
          ) : (
            filteredKeys.map((k) => {
              const isSearching =
                storedLookup.isPending &&
                lookupSource?.type === "stored" &&
                lookupSource.publicKey === k.publicKey;
              const showStoredResults =
                lookupSource?.type === "stored" &&
                lookupSource.publicKey === k.publicKey;

              return (
                <div
                  key={k.publicKey}
                  className="overflow-hidden rounded-lg border"
                >
                  <div className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0 flex-1 space-y-1.5">
                      <p className="break-all font-mono text-xs font-semibold leading-5 text-foreground sm:text-sm">
                        {k.publicKey}
                      </p>
                      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                        <span>
                          {k.accountCount} imported account{k.accountCount !== 1 && "s"}
                        </span>
                        {k.label ? (
                          <span className="rounded-md border bg-muted/50 px-1.5 py-0.5">
                            {k.label}
                          </span>
                        ) : (
                          <span>Unlabeled</span>
                        )}
                      </div>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={lookupPending || importPending}
                        onClick={() => handleStoredLookup(k.publicKey)}
                      >
                        {isSearching ? (
                          <Loader2 className="size-3.5 animate-spin" />
                        ) : (
                          <Search className="size-3.5" />
                        )}
                        Search
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        className="text-destructive hover:text-destructive"
                        disabled={removeKeyMutation.isPending || lookupPending || importPending}
                        onClick={() => setRemoveTarget(k)}
                        title="Remove key"
                        aria-label="Remove key"
                      >
                        <Trash2 className="size-3.5" />
                      </Button>
                    </div>
                  </div>

                  {showStoredResults && (
                    <div className="border-t bg-muted/20 p-3">
                      {isSearching ? (
                        <div className="flex items-center gap-2 rounded-lg border bg-background p-3 text-sm text-muted-foreground">
                          <Loader2 className="size-4 animate-spin" />
                          Searching linked accounts
                        </div>
                      ) : (
                        renderLookupResultsPanel()
                      )}
                    </div>
                  )}
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      {/* Account Lookup / Import */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Globe className="size-4 text-primary" />
            Search an Unstored Key
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleManualLookup} className="space-y-3">
            <div className="space-y-2">
              <Label htmlFor="lookup-pk">Private Key to Search</Label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="lookup-pk"
                  name="lookup-pk"
                  type="password"
                  placeholder="5K… or PVT_K1_…"
                  value={manualPrivateKey}
                  onChange={(e) => setManualPrivateKey(e.target.value)}
                  className="pl-9"
                  required
                />
              </div>
            </div>
            <Button
              type="submit"
              variant="outline"
              className="w-full"
              disabled={lookupPending || importPending || !manualPrivateKey.trim()}
            >
              {manualLookup.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Search className="size-4" />
              )}
              Search Accounts
            </Button>
          </form>
        </CardContent>
      </Card>

      {lookupSource?.type === "manual" && lookupResult && renderLookupResultsPanel()}

      <ConfirmDialog
        open={removeTarget !== null}
        onOpenChange={(open) => { if (!open) setRemoveTarget(null); }}
        title="Remove Key"
        description={
          removeTarget
            ? `Remove this key and its ${removeTarget.accountCount} linked account(s)? This cannot be undone.`
            : ""
        }
        confirmLabel="Remove"
        onConfirm={() => {
          if (!removeTarget) return;
          removeKeyMutation.mutate(
            { publicKey: removeTarget.publicKey },
            {
              onSuccess: () => toast.success("Key removed"),
              onError: () => toast.error("Failed to remove key"),
            },
          );
        }}
        disabled={removeKeyMutation.isPending}
      />
    </div>
  );
}
