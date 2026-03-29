import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Lock, Loader2, Wallet, Plus } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getHealth, unlockWallet, createWallet } from "@/api/client";

export default function Unlock() {
  const navigate = useNavigate();
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [walletExists, setWalletExists] = useState<boolean | null>(null);

  useEffect(() => {
    getHealth()
      .then((h) => {
        if (h.walletUnlocked) {
          navigate("/");
          return;
        }
        setWalletExists(h.walletLoaded);
      })
      .catch(() => setWalletExists(false));
  }, [navigate]);

  async function handleUnlock(e: React.FormEvent) {
    e.preventDefault();
    if (!password) return;

    setSubmitting(true);
    try {
      const res = await unlockWallet({ password });
      if (res.unlocked) {
        toast.success("Wallet unlocked");
        navigate("/");
      } else {
        toast.error("Incorrect password");
      }
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Unlock failed");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!password || password !== confirmPassword) {
      toast.error("Passwords do not match");
      return;
    }
    if (password.length < 8) {
      toast.error("Password must be at least 8 characters");
      return;
    }

    setSubmitting(true);
    try {
      const res = await createWallet({ password });
      if (res.unlocked) {
        toast.success("Wallet created");
        navigate("/");
      }
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Creation failed");
    } finally {
      setSubmitting(false);
    }
  }

  if (walletExists === null) return null;

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <div className="mx-auto mb-2 flex size-12 items-center justify-center rounded-full bg-primary/10">
            <Wallet className="size-6 text-primary" />
          </div>
          <CardTitle>Neo Wallet</CardTitle>
          <p className="text-sm text-muted-foreground">
            {walletExists
              ? "Enter your password to unlock"
              : "Create a password to secure your wallet"}
          </p>
        </CardHeader>
        <CardContent>
          {walletExists ? (
            <form onSubmit={handleUnlock} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="password"
                    type="password"
                    placeholder="Enter password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="pl-9"
                    autoFocus
                    required
                  />
                </div>
              </div>
              <Button type="submit" className="w-full" disabled={submitting}>
                {submitting && <Loader2 className="size-4 animate-spin" />}
                Unlock
              </Button>
            </form>
          ) : (
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="new-password">Password</Label>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="new-password"
                    type="password"
                    placeholder="At least 8 characters"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="pl-9"
                    autoFocus
                    required
                    minLength={8}
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="confirm-password">Confirm Password</Label>
                <div className="relative">
                  <Lock className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    id="confirm-password"
                    type="password"
                    placeholder="Repeat password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className="pl-9"
                    required
                    minLength={8}
                  />
                </div>
              </div>
              <Button type="submit" className="w-full" disabled={submitting}>
                {submitting && <Loader2 className="size-4 animate-spin" />}
                <Plus className="size-4" />
                Create Wallet
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
