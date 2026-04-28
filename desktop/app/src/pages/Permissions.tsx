import { useState } from "react";
import { KeyRound, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { ActionFormShell, requireActiveAccount } from "@/components/ActionFormShell";
import { useWalletSummary } from "@/api/hooks";
import type { SignAction } from "@/api/types";

interface KeyRow {
  key: string;
  weight: string;
}
interface AccountRow {
  actor: string;
  permission: string;
  weight: string;
}

/**
 * eosio::updateauth — modify an account's permission (active, owner, or custom).
 */
export default function Permissions() {
  const summary = useWalletSummary();
  const active = summary.data?.activeAccount;

  const [permission, setPermission] = useState("active");
  const [parent, setParent] = useState("owner");
  const [threshold, setThreshold] = useState("1");
  const [keys, setKeys] = useState<KeyRow[]>([{ key: "", weight: "1" }]);
  const [accounts, setAccounts] = useState<AccountRow[]>([]);

  function addKey() {
    setKeys([...keys, { key: "", weight: "1" }]);
  }
  function addAccount() {
    setAccounts([...accounts, { actor: "", permission: "active", weight: "1" }]);
  }

  return (
    <ActionFormShell
      title="Manage permissions"
      description="Update keys and account authority for a permission via eosio::updateauth"
      icon={<KeyRound className="size-5 text-primary" />}
      submitLabel="Update auth"
      buildActions={(): SignAction[] => {
        const a = requireActiveAccount(active);
        if (!permission.trim()) throw new Error("Permission name is required");
        const t = parseInt(threshold, 10);
        if (!Number.isFinite(t) || t < 1) throw new Error("Threshold must be >= 1");

        const keyRows = keys
          .filter((k) => k.key.trim())
          .map((k) => ({
            key: k.key.trim(),
            weight: parseInt(k.weight, 10) || 1,
          }))
          .sort((a, b) => a.key.localeCompare(b.key));

        const acctRows = accounts
          .filter((x) => x.actor.trim())
          .map((x) => ({
            permission: { actor: x.actor.trim(), permission: x.permission.trim() },
            weight: parseInt(x.weight, 10) || 1,
          }));

        if (keyRows.length === 0 && acctRows.length === 0)
          throw new Error("Provide at least one key or account");

        return [
          {
            account: "eosio",
            name: "updateauth",
            // updateauth must be authorized by the parent permission of the
            // permission being modified (or owner). Caller must use the right
            // active account / permission in the sidebar.
            data: {
              account: a.account,
              permission: permission.trim(),
              parent: parent.trim(),
              auth: {
                threshold: t,
                keys: keyRows,
                accounts: acctRows,
                waits: [],
              },
            },
          },
        ];
      }}
    >
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Permission</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1">
              <Label>Name</Label>
              <Input value={permission} onChange={(e) => setPermission(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Parent</Label>
              <Input value={parent} onChange={(e) => setParent(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Threshold</Label>
              <Input
                inputMode="numeric"
                value={threshold}
                onChange={(e) => setThreshold(e.target.value)}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between text-sm">
            Keys
            <Button type="button" variant="ghost" size="sm" onClick={addKey}>
              <Plus className="size-3.5" />
              Add
            </Button>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {keys.map((k, i) => (
            <div key={i} className="flex gap-2">
              <Input
                placeholder="EOS… / PUB_K1_…"
                className="flex-1 font-mono text-xs"
                value={k.key}
                onChange={(e) =>
                  setKeys(keys.map((x, idx) => (idx === i ? { ...x, key: e.target.value } : x)))
                }
              />
              <Input
                inputMode="numeric"
                className="w-20"
                value={k.weight}
                onChange={(e) =>
                  setKeys(
                    keys.map((x, idx) => (idx === i ? { ...x, weight: e.target.value } : x)),
                  )
                }
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => setKeys(keys.filter((_, idx) => idx !== i))}
              >
                <Trash2 className="size-3.5" />
              </Button>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between text-sm">
            Accounts
            <Button type="button" variant="ghost" size="sm" onClick={addAccount}>
              <Plus className="size-3.5" />
              Add
            </Button>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {accounts.length === 0 && (
            <p className="text-xs text-muted-foreground">None.</p>
          )}
          {accounts.map((a, i) => (
            <div key={i} className="flex gap-2">
              <Input
                placeholder="actor"
                className="flex-1"
                value={a.actor}
                onChange={(e) =>
                  setAccounts(
                    accounts.map((x, idx) =>
                      idx === i ? { ...x, actor: e.target.value } : x,
                    ),
                  )
                }
              />
              <Input
                placeholder="permission"
                className="flex-1"
                value={a.permission}
                onChange={(e) =>
                  setAccounts(
                    accounts.map((x, idx) =>
                      idx === i ? { ...x, permission: e.target.value } : x,
                    ),
                  )
                }
              />
              <Input
                inputMode="numeric"
                className="w-20"
                value={a.weight}
                onChange={(e) =>
                  setAccounts(
                    accounts.map((x, idx) =>
                      idx === i ? { ...x, weight: e.target.value } : x,
                    ),
                  )
                }
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => setAccounts(accounts.filter((_, idx) => idx !== i))}
              >
                <Trash2 className="size-3.5" />
              </Button>
            </div>
          ))}
        </CardContent>
      </Card>
    </ActionFormShell>
  );
}
