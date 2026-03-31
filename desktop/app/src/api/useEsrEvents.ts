import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "./hooks";
import type { EsrSseEvent } from "./types";

const BASE_URL = import.meta.env.VITE_BACKEND_URL ?? "";

/**
 * Hook that establishes a WebSocket connection to the backend ESR event stream.
 * Calls `onSigningRequest` when a dApp sends a signing request.
 * Automatically reconnects on disconnect.
 */
export function useEsrEvents(onSigningRequest?: (evt: EsrSseEvent) => void) {
  const qc = useQueryClient();
  const callbackRef = useRef(onSigningRequest);
  callbackRef.current = onSigningRequest;

  useEffect(() => {
    const token = sessionStorage.getItem("backend_token");
    if (!token) return;

    let ws: WebSocket | null = null;
    let reconnectTimer: ReturnType<typeof setTimeout>;
    let unmounted = false;

    function connect() {
      const wsBase = BASE_URL.replace(/^http/, "ws") || `ws://${window.location.host}`;
      ws = new WebSocket(`${wsBase}/api/esr/ws?token=${encodeURIComponent(token!)}`);

      ws.onmessage = (e: MessageEvent) => {
        try {
          const data = JSON.parse(e.data) as EsrSseEvent;

          if (data.type === "signing_request") {
            callbackRef.current?.(data);
            qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
          } else if (data.type === "status_changed") {
            qc.invalidateQueries({ queryKey: queryKeys.esrListenerStatus });
            qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
            callbackRef.current?.(data);
          }
        } catch { /* ignore parse errors */ }
      };

      ws.onclose = () => {
        if (!unmounted) {
          reconnectTimer = setTimeout(connect, 5_000);
        }
      };

      ws.onerror = () => {
        ws?.close();
      };
    }

    connect();

    return () => {
      unmounted = true;
      clearTimeout(reconnectTimer);
      ws?.close();
    };
  }, [qc]);
}
