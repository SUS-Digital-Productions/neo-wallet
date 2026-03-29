import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { FileCheck, Loader2, ShieldAlert, X } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import type { EsrParseResponse } from "@/api/types";
import { parseEsr, approveEsr, rejectEsr } from "@/api/client";

export default function EsrApproval() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const [uri, setUri] = useState(searchParams.get("uri") ?? "");
  const [parsed, setParsed] = useState<EsrParseResponse | null>(null);
  const [parsing, setParsing] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleParse(e: React.FormEvent) {
    e.preventDefault();
    if (!uri.trim()) return;

    setParsing(true);
    setParsed(null);
    try {
      const res = await parseEsr({ uri: uri.trim() });
      setParsed(res);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to parse ESR");
    } finally {
      setParsing(false);
    }
  }

  async function handleApprove() {
    if (!parsed) return;
    setSubmitting(true);
    try {
      const res = await approveEsr({
        requestId: parsed.requestId,
        broadcast: true,
      });
      toast.success(`Approved – tx ${res.transactionId.slice(0, 12)}…`);
      navigate("/");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Approval failed");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleReject() {
    if (!parsed) return;
    setSubmitting(true);
    try {
      await rejectEsr({
        requestId: parsed.requestId,
        reason: "User rejected",
      });
      toast.info("Request rejected");
      navigate("/");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Rejection failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Signing Request</h1>
        <p className="text-sm text-muted-foreground">
          Parse and approve EOSIO Signing Requests
        </p>
      </div>

      {/* Parse form */}
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
              <Input
                id="esr-uri"
                placeholder="esr://..."
                value={uri}
                onChange={(e) => setUri(e.target.value)}
                required
              />
            </div>
            <Button type="submit" variant="outline" disabled={parsing}>
              {parsing && <Loader2 className="size-4 animate-spin" />}
              Parse
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Parsed result */}
      {parsing && (
        <div className="space-y-2">
          <Skeleton className="h-32 w-full rounded-lg" />
        </div>
      )}

      {parsed && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldAlert className="size-4 text-destructive" />
              Review Actions
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

            <Separator />

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

            <Separator />

            <div className="flex gap-3">
              <Button
                onClick={handleApprove}
                className="flex-1"
                disabled={submitting}
              >
                {submitting && <Loader2 className="size-4 animate-spin" />}
                Approve & Broadcast
              </Button>
              <Button
                variant="outline"
                onClick={handleReject}
                disabled={submitting}
              >
                <X className="size-4" />
                Reject
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
