import { Copy, QrCode, UserX } from "lucide-react";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/EmptyState";
import { useWalletSummary } from "@/api/hooks";

export default function Receive() {
  const { data: summary, isLoading } = useWalletSummary();

  function copyToClipboard(text: string, label: string) {
    navigator.clipboard.writeText(text).then(
      () => toast.success(`${label} copied`),
      () => toast.error("Copy failed"),
    );
  }

  const account = summary?.activeAccount;
  const network = summary?.activeNetwork;

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Receive</h1>
        <p className="text-sm text-muted-foreground">
          Share your account details to receive tokens
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <QrCode className="size-4 text-primary" />
            Your Account
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="h-5 w-64" />
            </div>
          ) : !account ? (
            <EmptyState
              icon={UserX}
              title="No active account"
              description="Import an account to see your receive details"
            />
          ) : (
            <>
              {/* Account name */}
              <div className="space-y-1">
                <p className="text-xs uppercase text-muted-foreground">
                  Account
                </p>
                <div className="flex items-center gap-2">
                  <p className="text-lg font-semibold">{account.account}</p>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() =>
                      copyToClipboard(account.account, "Account name")
                    }
                  >
                    <Copy className="size-3.5" />
                  </Button>
                </div>
              </div>

              {/* Public key */}
              <div className="space-y-1">
                <p className="text-xs uppercase text-muted-foreground">
                  Public Key
                </p>
                <div className="flex items-center gap-2">
                  <p className="break-all font-mono text-xs">
                    {account.publicKey}
                  </p>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() =>
                      copyToClipboard(account.publicKey, "Public key")
                    }
                  >
                    <Copy className="size-3.5" />
                  </Button>
                </div>
              </div>

              <Separator />

              {/* Network */}
              {network && (
                <div className="flex items-center justify-between">
                  <p className="text-xs uppercase text-muted-foreground">
                    Network
                  </p>
                  <Badge variant="secondary">{network.name}</Badge>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
