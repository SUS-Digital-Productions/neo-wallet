import { useEffect, useRef, useState } from "react";
import { Html5Qrcode } from "html5-qrcode";
import { Camera, X } from "lucide-react";
import { Button } from "@/components/ui/button";

interface QrScannerProps {
  onScan: (data: string) => void;
}

export function QrScanner({ onScan }: QrScannerProps) {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scannerRef = useRef<Html5Qrcode | null>(null);
  const regionRef = useRef<HTMLDivElement>(null);
  const stoppedRef = useRef(false);

  useEffect(() => {
    if (!open) return;

    stoppedRef.current = false;
    const id = "qr-reader-region";
    const scanner = new Html5Qrcode(id);
    scannerRef.current = scanner;

    scanner
      .start(
        { facingMode: "environment" },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (text) => {
          if (stoppedRef.current) return;
          stoppedRef.current = true;
          scanner
            .stop()
            .then(() => scanner.clear())
            .catch(() => {});
          onScan(text);
          setOpen(false);
        },
        () => {},
      )
      .catch((err) => {
        setError(
          typeof err === "string"
            ? err
            : err?.message ?? "Camera access denied",
        );
      });

    return () => {
      stoppedRef.current = true;
      scanner
        .stop()
        .then(() => scanner.clear())
        .catch(() => {});
    };
  }, [open, onScan]);

  if (!open) {
    return (
      <Button
        type="button"
        variant="outline"
        size="icon"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        title="Scan QR code"
      >
        <Camera className="size-4" />
      </Button>
    );
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium">Scan QR Code</p>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-7"
          onClick={() => setOpen(false)}
        >
          <X className="size-4" />
        </Button>
      </div>
      <div
        id="qr-reader-region"
        ref={regionRef}
        className="overflow-hidden rounded-lg border"
      />
      {error && (
        <p className="text-xs text-destructive">{error}</p>
      )}
    </div>
  );
}
