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
} from "./types";

const BASE_URL = import.meta.env.VITE_BACKEND_URL ?? "";

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
