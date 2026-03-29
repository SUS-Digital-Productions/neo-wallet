import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { KeyRound, Loader2, Import as ImportIcon } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { importAccount } from "@/api/client";

export default function ImportAccount() {
  const navigate = useNavigate();

  const [privateKey, setPrivateKey] = useState("");
  const [account, setAccount] = useState("");
  const [authority, setAuthority] = useState("active");
  const [password, setPassword] = useState("");
  const [importing, setImporting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!privateKey || !account || !password) return;

    setImporting(true);
    try {
      const dto = await importAccount({
        privateKey,
        account,
        authority: authority || "active",
        password,
      });
      toast.success(`Imported ${dto.account}@${dto.authority}`);
      navigate("/");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Import failed");
    } finally {
      setImporting(false);
    }
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Import Account</h1>
        <p className="text-sm text-muted-foreground">
          Add an account by private key
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ImportIcon className="size-4 text-primary" />
            Import
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
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
              <Label htmlFor="acct">Account Name</Label>
              <Input
                id="acct"
                placeholder="e.g. myaccount123"
                value={account}
                onChange={(e) => setAccount(e.target.value)}
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="auth">Permission</Label>
              <Input
                id="auth"
                placeholder="active"
                value={authority}
                onChange={(e) => setAuthority(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="pwd">Wallet Password</Label>
              <Input
                id="pwd"
                type="password"
                placeholder="Your wallet password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>

            <Button type="submit" className="w-full" disabled={importing}>
              {importing && <Loader2 className="size-4 animate-spin" />}
              Import Account
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
