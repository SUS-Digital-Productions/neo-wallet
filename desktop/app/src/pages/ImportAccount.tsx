import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  KeyRound,
  Loader2,
  Import as ImportIcon,
  Search,
  Check,
  Globe,
  SearchX,
} from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { EmptyState } from "@/components/EmptyState";
import type {
  LookupAccountsResponse,
  ImportAccountEntry,
} from "@/api/types";
import { useLookupAccounts, useImportAccounts } from "@/api/hooks";

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

export default function ImportAccount() {
  const navigate = useNavigate();
  const lookup = useLookupAccounts();
  const importMutation = useImportAccounts();

  const [privateKey, setPrivateKey] = useState("");
  const [lookupResult, setLookupResult] = useState<LookupAccountsResponse | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  function entryKey(chainId: string, account: string, authority: string) {
    return `${chainId}::${account}::${authority}`;
  }

  function toggleSelection(key: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function selectAll() {
    if (!lookupResult) return;
    const all = new Set<string>();
    for (const chain of lookupResult.chains) {
      for (const acct of chain.accounts) {
        all.add(entryKey(chain.chainId, acct.account, acct.authority));
      }
    }
    setSelected(all);
  }

  function handleLookup(e: React.FormEvent) {
    e.preventDefault();
    if (!privateKey.trim()) return;

    setLookupResult(null);
    setSelected(new Set());

    lookup.mutate(
      { privateKey },
      {
        onSuccess: (result) => {
          setLookupResult(result);
          const all = new Set<string>();
          for (const chain of result.chains) {
            for (const acct of chain.accounts) {
              all.add(entryKey(chain.chainId, acct.account, acct.authority));
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
    if (!lookupResult || selected.size === 0) return;

    const accounts: ImportAccountEntry[] = [];
    for (const key of selected) {
      const [chainId, account, authority] = key.split("::");
      accounts.push({ chainId, account, authority });
    }

    importMutation.mutate(
      { privateKey, accounts },
      {
        onSuccess: (imported) => {
          toast.success(`Imported ${imported.length} account(s)`);
          navigate("/");
        },
        onError: (err) => {
          toast.error(err instanceof Error ? err.message : "Import failed");
        },
      },
    );
  }

  const totalFound =
    lookupResult?.chains.reduce((sum, c) => sum + c.accounts.length, 0) ?? 0;

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Import Account</h1>
        <p className="text-sm text-muted-foreground">
          Enter your private key to find and import linked accounts
        </p>
      </div>

      {/* Step 1: Enter key and search */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <KeyRound className="size-4 text-primary" />
            Private Key
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleLookup} className="space-y-4">
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

            <Button
              type="submit"
              className="w-full"
              disabled={lookup.isPending || !privateKey.trim()}
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

      {/* Step 2: Results */}
      {lookupResult && (
        <>
          {/* Public key */}
          <Card>
            <CardContent className="py-3">
              <p className="text-xs text-muted-foreground">Derived Public Key</p>
              <p className="font-mono text-xs break-all">
                {lookupResult.publicKey}
              </p>
            </CardContent>
          </Card>

          {/* Chain results */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Globe className="size-4 text-primary" />
                  Found Accounts
                </CardTitle>
                {totalFound > 0 && (
                  <Button variant="ghost" size="sm" onClick={selectAll}>
                    Select All
                  </Button>
                )}
              </div>
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
                          const isSelected = selected.has(key);
                          return (
                            <button
                              key={key}
                              type="button"
                              onClick={() => toggleSelection(key)}
                              className={`flex w-full items-center justify-between rounded-md border px-3 py-2 text-left transition-colors ${
                                isSelected
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
                              {isSelected && (
                                <Check className="size-4 text-primary" />
                              )}
                            </button>
                          );
                        })}
                      </div>
                    )}
                    <Separator className="mt-3" />
                  </div>
                ))
              )}

              {totalFound > 0 && (
                <Button
                  className="w-full"
                  disabled={importMutation.isPending || selected.size === 0}
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
    </div>
  );
}
