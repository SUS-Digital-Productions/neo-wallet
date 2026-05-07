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
  node: string;
}

export interface SetNetworkNodeRequest {
  chainId: string;
  node: string;
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
  /** Configured auto-lock timeout in minutes (0 = disabled). */
  autoLockMinutes: number;
  /** ISO-8601 UTC timestamp of when the wallet will auto-lock, or null if unlocked-but-disabled / locked. */
  lockExpiresAt: string | null;
  walletUnlocked: boolean;
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

export interface GetPrivateKeyRequest {
  account: string;
  authority: string;
  chainId?: string;
}

export interface GetPrivateKeyResponse {
  privateKey: string;
}

export interface ImportWalletRequest {
  password: string;
  fileBase64: string;
}

export interface ImportAnchorWalletRequest {
  password: string;
  fileBase64: string;
}

export interface ImportAnchorWalletResponse {
  importedKeys: number;
  publicKeys: string[];
  format: string;
}

/* ---- Keys ---- */

export interface KeyInfo {
  publicKey: string;
  label: string;
  accountCount: number;
}

export interface AddKeyRequest {
  privateKey: string;
  label: string;
}

export interface RemoveKeyRequest {
  publicKey: string;
}

export interface LookupStoredKeyAccountsRequest {
  publicKey: string;
  chainIds?: string[];
}

export interface ImportStoredKeyAccountsRequest {
  publicKey: string;
  accounts: ImportAccountEntry[];
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
  contract: string;
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
  account?: string;
  authority?: string;
  chainId?: string;
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
  account?: string;
  authority?: string;
}

/* ---- ESR Listener ---- */

export interface EsrListenerStatus {
  status: string;
  linkId: string;
  requestPublicKey: string | null;
  sessionCount: number;
}

export interface EsrSigningRequestEvent {
  type: "signing_request";
  requestId?: string;
  isIdentity: boolean;
  chainId?: string;
  actions?: EsrActionSummary[];
  session: {
    actor: string;
    permission: string;
    chainId: string;
    name: string;
  } | null;
  callbackUrl: string | null;
  rawPayload: string | null;
}

export interface EsrStatusChangedEvent {
  type: "status_changed";
  status: string;
}

export type EsrSseEvent = EsrSigningRequestEvent | EsrStatusChangedEvent;

/* ---- Generic action signing (used by Sign Action page + dedicated forms) ---- */

export interface SignAction {
  account: string;
  name: string;
  data: Record<string, unknown>;
  authorization?: { actor: string; permission: string }[];
}

export interface SignActionsRequest {
  chainId?: string;
  actions: SignAction[];
  broadcast?: boolean;
}

export interface SignActionsResponse {
  transactionId: string;
  broadcast: boolean;
}

/* ---- Chain (Account Viewer) ----
 * The /api/chain/account endpoint returns the raw nodeos /v1/chain/get_account
 * response, which is large and chain-specific. We type only the bits we render.
 */

export interface ChainResourceLimit {
  used: number;
  available: number;
  max: number;
}

export interface ChainKeyWeight {
  key: string;
  weight: number;
}

export interface ChainPermissionLevel {
  actor: string;
  permission: string;
}

export interface ChainAccountInfo {
  account_name: string;
  created: string;
  ram_quota: number;
  ram_usage: number;
  net_weight: number | string;
  cpu_weight: number | string;
  net_limit: ChainResourceLimit;
  cpu_limit: ChainResourceLimit;
  core_liquid_balance?: string;
  permissions: {
    perm_name: string;
    parent: string;
    required_auth: {
      threshold: number;
      keys: ChainKeyWeight[];
      accounts: { permission: ChainPermissionLevel; weight: number }[];
      waits: { wait_sec: number; weight: number }[];
    };
  }[];
  total_resources?: {
    owner: string;
    net_weight: string;
    cpu_weight: string;
    ram_bytes: number;
  } | null;
  self_delegated_bandwidth?: {
    from: string;
    to: string;
    net_weight: string;
    cpu_weight: string;
  } | null;
  refund_request?: {
    owner: string;
    request_time: string;
    net_amount: string;
    cpu_amount: string;
  } | null;
  voter_info?: {
    owner: string;
    proxy: string;
    producers: string[];
    staked: number | string;
    last_vote_weight: string;
    proxied_vote_weight: string;
    is_proxy: number;
  } | null;
  rex_info?: {
    owner: string;
    vote_stake: string;
    rex_balance: string;
    matured_rex: number;
  } | null;
  // Allow forward-compatibility for unknown fields.
  [extraField: string]: unknown;
}
