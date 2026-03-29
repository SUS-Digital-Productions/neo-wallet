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
}

export interface ImportAccountRequest {
  privateKey: string;
  account: string;
  authority: string;
  password: string;
}

export interface RemoveAccountRequest {
  account: string;
  authority: string;
  password: string;
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
