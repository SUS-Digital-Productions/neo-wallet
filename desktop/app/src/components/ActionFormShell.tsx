import { useState } from "react";
import { Send, AlertCircle } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useSignActions, useWalletSummary } from "@/api/hooks";
import type { SignAction } from "@/api/types";

interface Props {
  title: string;
  description: string;
  icon: React.ReactNode;
  /** Build the action(s) from current form state. Throw to abort. */
  buildActions: () => SignAction[];
  /** Form fields */
  children: React.ReactNode;
  /** Optional submit-button label (defaults to "Sign & Broadcast"). */
  submitLabel?: string;
  onSuccess?: (txId: string) => void;
}

/**
 * Reusable shell for the dedicated system-action forms (Stake, RAM, Vote,
 * PowerUp, Claim, Bid, Permissions, Create Account).
 * Renders the title, the form fields, and a submit button that calls
 * /api/actions/sign with the actions returned by buildActions().
 */
export function ActionFormShell({
  title,
  description,
  icon,
  buildActions,
  children,
  submitLabel = "Sign & Broadcast",
  onSuccess,
}: Props) {
  const summary = useWalletSummary();
  const sign = useSignActions();
  const [lastTx, setLastTx] = useState<string | null>(null);
  const active = summary.data?.activeAccount;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLastTx(null);
    try {
      const actions = buildActions();
      if (actions.length === 0) {
        toast.error("Nothing to sign");
        return;
      }
      const res = await sign.mutateAsync({ actions, broadcast: true });
      setLastTx(res.transactionId);
      toast.success(`Broadcast: ${res.transactionId.slice(0, 12)}…`);
      onSuccess?.(res.transactionId);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err));
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-xl space-y-5">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          {icon}
          {title}
        </h1>
        <p className="text-sm text-muted-foreground">{description}</p>
      </div>

      {!active && (
        <Card>
          <CardContent className="flex items-center gap-3 py-4 text-sm">
            <AlertCircle className="size-5 text-destructive" />
            <p>No active account. Pick one from the sidebar first.</p>
          </CardContent>
        </Card>
      )}

      {children}

      <div className="flex items-center justify-between gap-3">
        {lastTx ? (
          <p className="break-all font-mono text-[11px] text-muted-foreground">
            tx: {lastTx}
          </p>
        ) : (
          <span />
        )}
        <Button type="submit" disabled={sign.isPending || !active}>
          <Send className="size-4" />
          {sign.isPending ? "Signing…" : submitLabel}
        </Button>
      </div>
    </form>
  );
}

/**
 * Returns the active account info or throws an error suitable for buildActions().
 */
export function requireActiveAccount(active: { account: string; authority: string } | null | undefined) {
  if (!active) throw new Error("No active account");
  return active;
}
