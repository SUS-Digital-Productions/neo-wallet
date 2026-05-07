import type {
  HealthResponse,
  WalletSummary,
  AccountInfo,
  SetActiveAccountRequest,
  NetworkInfo,
  BalanceEntry,
  CreateWalletRequest,
  UnlockRequest,
  UnlockResponse,
  ImportAccountRequest,
  RemoveAccountRequest,
  TransferRequest,
  TransferResponse,
  EsrParseRequest,
  EsrParseResponse,
  EsrApproveRequest,
  EsrRejectRequest,
  LookupAccountsRequest,
  LookupAccountsResponse,
  AutoLockSettings,
  AppSettings,
  SignRawRequest,
  EsrListenerStatus,
  GetPrivateKeyRequest,
  GetPrivateKeyResponse,
  ImportWalletRequest,
  KeyInfo,
  AddKeyRequest,
  RemoveKeyRequest,
  LookupStoredKeyAccountsRequest,
  ImportStoredKeyAccountsRequest,
  SignActionsRequest,
  SignActionsResponse,
  ChainAccountInfo,
  ImportAnchorWalletRequest,
  ImportAnchorWalletResponse,
  SetNetworkNodeRequest,
} from "./types";

/**
 * Resolve the backend base URL:
 * - If VITE_BACKEND_URL is set (dev), use it.
 * - On Tauri mobile the webview origin is not the backend, so target localhost explicitly.
 * - Desktop production: the .NET backend serves the frontend, so relative paths work.
 */
function resolveBaseUrl(): string {
  const env = import.meta.env.VITE_BACKEND_URL;
  if (env) return env as string;

  // Tauri mobile: webview is loaded from tauri:// or https://tauri.localhost,
  // not from the backend, so we must use the explicit embedded-server address.
  if (
    (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ &&
    !window.location.origin.includes("localhost:5199")
  ) {
    return "http://127.0.0.1:5199";
  }

  return "";
}

const BASE_URL = resolveBaseUrl();

function token(): string | null {
  return sessionStorage.getItem("backend_token");
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init?.headers as Record<string, string>),
  };
  const t = token();
  if (t) headers["Authorization"] = `Bearer ${t}`;

  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(body || `${res.status} ${res.statusText}`);
  }
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

/* ---- Health ---- */
export const getHealth = () => request<HealthResponse>("/api/health");

/* ---- Wallet ---- */
export const getWalletSummary = () =>
  request<WalletSummary>("/api/wallet/summary");

export const createWallet = (body: CreateWalletRequest) =>
  request<UnlockResponse>("/api/wallet/create", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const unlockWallet = (body: UnlockRequest) =>
  request<UnlockResponse>("/api/wallet/unlock", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const lockWallet = () =>
  request<void>("/api/wallet/lock", { method: "POST" });

export async function exportWallet(): Promise<Blob> {
  const t = token();
  const headers: Record<string, string> = {};
  if (t) headers["Authorization"] = `Bearer ${t}`;
  const res = await fetch(`${BASE_URL}/api/wallet/export`, { headers });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.blob();
}

export const importWallet = (body: ImportWalletRequest) =>
  request<UnlockResponse>("/api/wallet/import", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const importAnchorWallet = (body: ImportAnchorWalletRequest) =>
  request<ImportAnchorWalletResponse>("/api/wallet/import-anchor", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const getPrivateKey = (body: GetPrivateKeyRequest) =>
  request<GetPrivateKeyResponse>("/api/accounts/private-key", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Keys ---- */
export const getKeys = () => request<KeyInfo[]>("/api/keys");

export const addKey = (body: AddKeyRequest) =>
  request<KeyInfo>("/api/keys", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const removeKey = (body: RemoveKeyRequest) =>
  request<void>("/api/keys/remove", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const lookupStoredKeyAccounts = (body: LookupStoredKeyAccountsRequest) =>
  request<LookupAccountsResponse>("/api/keys/lookup", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const importStoredKeyAccounts = (body: ImportStoredKeyAccountsRequest) =>
  request<AccountInfo[]>("/api/keys/import-accounts", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Accounts ---- */
export const getAccounts = () => request<AccountInfo[]>("/api/accounts");

export const lookupAccounts = (body: LookupAccountsRequest) =>
  request<LookupAccountsResponse>("/api/accounts/lookup", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const importAccount = (body: ImportAccountRequest) =>
  request<AccountInfo[]>("/api/accounts/import", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const removeAccount = (body: RemoveAccountRequest) =>
  request<void>("/api/accounts/remove", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const setActiveAccount = (body: SetActiveAccountRequest) =>
  request<void>("/api/accounts/active", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Networks ---- */
export const getNetworks = () => request<NetworkInfo[]>("/api/networks");

export const setActiveNetwork = (chainId: string) =>
  request<void>("/api/networks/active", {
    method: "POST",
    body: JSON.stringify({ chainId }),
  });

export const setNetworkNode = (body: SetNetworkNodeRequest) =>
  request<void>("/api/networks/node", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Balances ---- */
export const getBalances = (account: string, chainId: string) =>
  request<BalanceEntry[]>(
    `/api/balances?account=${encodeURIComponent(account)}&chainId=${encodeURIComponent(chainId)}`,
  );

/* ---- Transfers ---- */
export const sendTransfer = (body: TransferRequest) =>
  request<TransferResponse>("/api/transfers", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- ESR ---- */
export const parseEsr = (body: EsrParseRequest) =>
  request<EsrParseResponse>("/api/esr/parse", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const getPendingEsr = (requestId: string) =>
  request<EsrParseResponse>(`/api/esr/pending/${encodeURIComponent(requestId)}`);

export const approveEsr = (body: EsrApproveRequest) =>
  request<TransferResponse>("/api/esr/approve", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const rejectEsr = (body: EsrRejectRequest) =>
  request<void>("/api/esr/reject", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const signRawTransaction = (body: SignRawRequest) =>
  request<TransferResponse>("/api/esr/sign-raw", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Settings ---- */
export const getAutoLockSettings = () =>
  request<AutoLockSettings>("/api/settings/autolock");

export const setAutoLockSettings = (body: AutoLockSettings) =>
  request<AutoLockSettings>("/api/settings/autolock", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const getAppSettings = () =>
  request<AppSettings>("/api/settings/app");

export const setAppSettings = (body: AppSettings) =>
  request<AppSettings>("/api/settings/app", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- ESR Listener ---- */
export const getEsrListenerStatus = () =>
  request<EsrListenerStatus>("/api/esr/listener/status");

export const connectEsrListener = () =>
  request<{ status: string }>("/api/esr/listener/connect", { method: "POST" });

export const disconnectEsrListener = () =>
  request<{ status: string }>("/api/esr/listener/disconnect", { method: "POST" });

/* ---- Generic Action Signing ---- */
export const signActions = (body: SignActionsRequest) =>
  request<SignActionsResponse>("/api/actions/sign", {
    method: "POST",
    body: JSON.stringify(body),
  });

/* ---- Chain (Account Viewer + utility queries) ---- */
export const getChainAccount = (account: string, chainId: string) =>
  request<ChainAccountInfo>(
    `/api/chain/account?account=${encodeURIComponent(account)}&chainId=${encodeURIComponent(chainId)}`,
  );

export const getCurrencyBalance = (
  account: string,
  chainId: string,
  contract?: string,
  symbol?: string,
) => {
  const qs = new URLSearchParams({ account, chainId });
  if (contract) qs.set("contract", contract);
  if (symbol) qs.set("symbol", symbol);
  return request<string[]>(`/api/chain/currency-balance?${qs.toString()}`);
};

export const getTableRows = (body: Record<string, unknown> & { chainId: string }) =>
  request<{ rows: unknown[]; more: boolean; next_key?: string }>(
    "/api/chain/table-rows",
    { method: "POST", body: JSON.stringify(body) },
  );
