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
  Import as ImportIcon,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/EmptyState";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import type {
  LookupAccountsResponse,
  ImportAccountEntry,
  KeyInfo,
} from "@/api/types";
import {
  useKeys,
  useAddKey,
  useRemoveKey,
  useLookupAccounts,
  useImportAccounts,
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

export default function Keys() {
  const { data: keys = [], isLoading: keysLoading } = useKeys();
  const { data: existingAccounts = [] } = useAccounts();
  const addKeyMutation = useAddKey();
  const removeKeyMutation = useRemoveKey();
  const lookup = useLookupAccounts();
  const importMutation = useImportAccounts();

  // Add key form
  const [privateKey, setPrivateKey] = useState("");
  const [label, setLabel] = useState("");
  const [removeTarget, setRemoveTarget] = useState<KeyInfo | null>(null);

  // Lookup state
  const [lookupKey, setLookupKey] = useState("");
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

  function handleLookup(wif: string) {
    setLookupKey(wif);
    setLookupResult(null);
    setSelected(new Set());

    lookup.mutate(
      { privateKey: wif },
      {
        onSuccess: (result) => {
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
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : "Lookup failed");
        },
      },
    );
  }

  function handleImport() {
    if (!lookupResult || selected.size === 0 || !lookupKey) return;

    const accounts: ImportAccountEntry[] = [];
    for (const key of selected) {
      const [chainId, account, authority] = key.split("::");
      accounts.push({ chainId, account, authority });
    }

    importMutation.mutate(
      { privateKey: lookupKey, accounts },
      {
        onSuccess: (imported) => {
          toast.success(`Imported ${imported.length} account(s)`);
          setLookupResult(null);
          setSelected(new Set());
          setLookupKey("");
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : "Import failed"),
      },
    );
  }

  const totalFound =
    lookupResult?.chains.reduce((sum, c) => sum + c.accounts.length, 0) ?? 0;

  return (
    <div className="mx-auto max-w-lg space-y-6">
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
        <CardContent className="space-y-2">
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
          ) : (
            keys.map((k) => (
              <div
                key={k.publicKey}
                className="flex items-center justify-between rounded-lg border px-4 py-3"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">
                    {k.label || "Unlabeled"}
                  </p>
                  <p className="truncate font-mono text-xs text-muted-foreground">
                    {k.publicKey}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {k.accountCount} linked account{k.accountCount !== 1 && "s"}
                  </p>
                </div>
                <div className="ml-2 flex items-center gap-1">
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-8 text-destructive hover:text-destructive"
                    disabled={removeKeyMutation.isPending}
                    onClick={() => setRemoveTarget(k)}
                  >
                    <Trash2 className="size-3.5" />
                  </Button>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {/* Account Lookup / Import */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Globe className="size-4 text-primary" />
            Find & Import Accounts
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const wif = (e.currentTarget.elements.namedItem("lookup-pk") as HTMLInputElement)?.value;
              if (wif?.trim()) handleLookup(wif.trim());
            }}
            className="space-y-3"
          >
            <div className="space-y-2">
              <Label htmlFor="lookup-pk">Private Key to Search</Label>
              <div className="relative">
                <KeyRound className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="lookup-pk"
                  name="lookup-pk"
                  type="password"
                  placeholder="5K… or PVT_K1_…"
                  className="pl-9"
                  required
                />
              </div>
            </div>
            <Button
              type="submit"
              variant="outline"
              className="w-full"
              disabled={lookup.isPending}
            >
              {lookup.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Search className="size-4" />
              )}
              Search Accounts
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Lookup Results */}
      {lookupResult && (
        <>
          <Card>
            <CardContent className="py-3">
              <p className="text-xs text-muted-foreground">Derived Public Key</p>
              <p className="break-all font-mono text-xs">{lookupResult.publicKey}</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Globe className="size-4 text-primary" />
                Found Accounts
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {totalFound === 0 ? (
                <EmptyState
                  icon={SearchX}
                  title="No accounts found"
                  description="This key has no linked accounts on any supported chain"
                />
              ) : (
                lookupResult.chains.map((chain) => (
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
                                  ? "border-border bg-muted/50 opacity-60 cursor-not-allowed"
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
                    <Separator className="mt-3" />
                  </div>
                ))
              )}

              {totalFound > 0 && selected.size > 0 && (
                <Button
                  className="w-full"
                  disabled={importMutation.isPending}
                  onClick={handleImport}
                >
                  {importMutation.isPending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <ImportIcon className="size-4" />
                  )}
                  Import {selected.size} Account{selected.size !== 1 && "s"}
                </Button>
              )}
            </CardContent>
          </Card>
        </>
      )}

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
