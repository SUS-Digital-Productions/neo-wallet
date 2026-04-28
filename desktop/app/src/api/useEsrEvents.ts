import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "./hooks";
import type { EsrSseEvent } from "./types";

function resolveBaseUrl(): string {
  const env = import.meta.env.VITE_BACKEND_URL;
  if (env) return env as string;
  if (
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ &&
    !window.location.origin.includes("localhost:5199")
  ) {
    return "http://127.0.0.1:5199";
  }
  return "";
}

const BASE_URL = resolveBaseUrl();

/**
 * Best-effort: bring the Tauri window to the foreground.
 * No-ops gracefully when running outside Tauri (plain browser dev).
 */
async function focusAppWindow(): Promise<void> {
  try {
    const { invoke } = await import("@tauri-apps/api/core");
    await invoke("show_main_window");
  } catch {
    // not running inside Tauri, or command not registered
  }
}

/**
 * Hook that establishes a WebSocket connection to the backend ESR event stream.
 * Calls `onSigningRequest` when a dApp sends a signing request.
 * Automatically:
 *   - Focuses / un-minimises the Tauri window so the user sees the prompt.
 *   - Reconnects every 5 seconds on disconnect.
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
            // Pop the wallet to the foreground BEFORE calling the page-level
            // handler so the user sees the toast/navigation immediately.
            void focusAppWindow();
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
