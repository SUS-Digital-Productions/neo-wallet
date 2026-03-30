/* ---- Health ---- */

export interface HealthResponse {
  status: string;
  version: string;
  walletLoaded: boolean;
  walletUnlocked: boolean;
}

/* ---- Network ---- */

export interface NetworkInfo {
  chainId: string;
  name: string;
  symbol: string;
}

/* ---- Account ---- */

export interface AccountInfo {
  account: string;
  authority: string;
  publicKey: string;
  chainId: string;
  chainName: string;
}

export interface SetActiveAccountRequest {
  account: string;
  authority: string;
  chainId: string;
}

/* ---- Wallet ---- */

export interface WalletSummary {
  activeNetwork: NetworkInfo | null;
  activeAccount: AccountInfo | null;
  listenerStatus: string;
}

export interface CreateWalletRequest {
  password: string;
}

export interface UnlockRequest {
  password: string;
}

export interface UnlockResponse {
  unlocked: boolean;
  token?: string;
}

export interface ImportAccountEntry {
  account: string;
  authority: string;
  chainId: string;
}

export interface ImportAccountRequest {
  privateKey: string;
  accounts: ImportAccountEntry[];
}

export interface RemoveAccountRequest {
  account: string;
  authority: string;
  chainId: string;
}

/* ---- Lookup ---- */

export interface LookupAccountsRequest {
  privateKey: string;
  chainIds?: string[];
}

export interface LookupAccountEntry {
  account: string;
  authority: string;
}

export interface LookupChainResult {
  chainId: string;
  name: string;
  symbol: string;
  accounts: LookupAccountEntry[];
}

export interface LookupAccountsResponse {
  publicKey: string;
  chains: LookupChainResult[];
}

/* ---- Settings ---- */

export interface AutoLockSettings {
  timeoutMinutes: number;
}

export interface AppSettings {
  startAtLogin: boolean;
  minimizeToTray: boolean;
}

/* ---- Balances ---- */

export interface BalanceEntry {
  symbol: string;
  amount: string;
  numericAmount: number;
}

/* ---- Transfer ---- */

export interface TransferRequest {
  chainId: string;
  from: string;
  authority: string;
  to: string;
  quantity: string;
  memo?: string;
}

export interface TransferResponse {
  transactionId: string;
  broadcast: boolean;
}

/* ---- ESR ---- */

export interface EsrParseRequest {
  uri: string;
}

export interface EsrActionSummary {
  account: string;
  name: string;
}

export interface EsrParseResponse {
  requestId: string;
  chainId: string;
  type: string;
  actions: EsrActionSummary[];
}

export interface EsrApproveRequest {
  requestId: string;
  broadcast: boolean;
}

export interface EsrRejectRequest {
  requestId: string;
  reason?: string;
}

/* ---- Sign Raw ---- */

export interface SignRawAction {
  account: string;
  name: string;
  data: Record<string, unknown>;
}

export interface SignRawRequest {
  chainId: string;
  actions: SignRawAction[];
  broadcast?: boolean;
}
