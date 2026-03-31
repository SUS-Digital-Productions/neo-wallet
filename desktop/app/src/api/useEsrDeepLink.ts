import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

/**
 * Listens for `esr://` deep-link events emitted by the Tauri shell.
 * When an ESR URI arrives, navigates to `/esr?uri=<encoded>` so the
 * EsrApproval page auto-parses the request.
 *
 * Gracefully no-ops when running outside Tauri (plain browser dev).
 */
export function useEsrDeepLink() {
  const navigate = useNavigate();

  useEffect(() => {
    let unlisten: (() => void) | undefined;

    async function init() {
      try {
        // Dynamic import so the app still works without Tauri runtime
        const { listen } = await import("@tauri-apps/api/event");

        const unlistenFn = await listen<string>("esr-deep-link", (event) => {
          const uri = event.payload;
          if (uri) {
            navigate(`/esr?uri=${encodeURIComponent(uri)}`);
          }
        });

        unlisten = unlistenFn;
      } catch {
        // Not running inside Tauri – silently ignore
      }
    }

    init();

    return () => {
      unlisten?.();
    };
  }, [navigate]);
}
