import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryOptions,
} from "@tanstack/react-query";
import * as api from "./client";
import type {
  HealthResponse,
  WalletSummary,
  AccountInfo,
  NetworkInfo,
  BalanceEntry,
  AutoLockSettings,
  AppSettings,
  LookupAccountsRequest,
  ImportAccountRequest,
  RemoveAccountRequest,
  SetActiveAccountRequest,
  TransferRequest,
  EsrParseRequest,
  EsrApproveRequest,
  EsrRejectRequest,
  SignRawRequest,
  CreateWalletRequest,
  UnlockRequest,
} from "./types";

// ── Query Keys ──────────────────────────────────────────

export const queryKeys = {
  health: ["health"] as const,
  walletSummary: ["wallet", "summary"] as const,
  accounts: ["accounts"] as const,
  networks: ["networks"] as const,
  balances: (account: string, chainId: string) =>
    ["balances", account, chainId] as const,
  autoLockSettings: ["settings", "autolock"] as const,
  appSettings: ["settings", "app"] as const,
};

// ── Queries ─────────────────────────────────────────────

export function useHealth(
  opts?: Partial<UseQueryOptions<HealthResponse>>
) {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: api.getHealth,
    ...opts,
  });
}

export function useWalletSummary(
  opts?: Partial<UseQueryOptions<WalletSummary>>
) {
  return useQuery({
    queryKey: queryKeys.walletSummary,
    queryFn: api.getWalletSummary,
    ...opts,
  });
}

export function useAccounts(
  opts?: Partial<UseQueryOptions<AccountInfo[]>>
) {
  return useQuery({
    queryKey: queryKeys.accounts,
    queryFn: api.getAccounts,
    ...opts,
  });
}

export function useNetworks(
  opts?: Partial<UseQueryOptions<NetworkInfo[]>>
) {
  return useQuery({
    queryKey: queryKeys.networks,
    queryFn: api.getNetworks,
    ...opts,
  });
}

export function useBalances(
  account: string | undefined,
  chainId: string | undefined,
  opts?: Partial<UseQueryOptions<BalanceEntry[]>>
) {
  return useQuery({
    queryKey: queryKeys.balances(account ?? "", chainId ?? ""),
    queryFn: () => api.getBalances(account!, chainId!),
    enabled: !!account && !!chainId,
    ...opts,
  });
}

export function useAutoLockSettings(
  opts?: Partial<UseQueryOptions<AutoLockSettings>>
) {
  return useQuery({
    queryKey: queryKeys.autoLockSettings,
    queryFn: api.getAutoLockSettings,
    ...opts,
  });
}

export function useAppSettings(
  opts?: Partial<UseQueryOptions<AppSettings>>
) {
  return useQuery({
    queryKey: queryKeys.appSettings,
    queryFn: api.getAppSettings,
    ...opts,
  });
}

// ── Mutations ───────────────────────────────────────────

export function useCreateWallet() {
  return useMutation({
    mutationFn: (body: CreateWalletRequest) => api.createWallet(body),
  });
}

export function useUnlockWallet() {
  return useMutation({
    mutationFn: (body: UnlockRequest) => api.unlockWallet(body),
  });
}

export function useLockWallet() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.lockWallet,
    onSuccess: () => qc.clear(),
  });
}

export function useLookupAccounts() {
  return useMutation({
    mutationFn: (body: LookupAccountsRequest) => api.lookupAccounts(body),
  });
}

export function useImportAccounts() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ImportAccountRequest) => api.importAccount(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.accounts });
      qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
    },
  });
}

export function useRemoveAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RemoveAccountRequest) => api.removeAccount(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.accounts });
      qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
    },
  });
}

export function useSetActiveAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SetActiveAccountRequest) => api.setActiveAccount(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
    },
  });
}

export function useSetActiveNetwork() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (chainId: string) => api.setActiveNetwork(chainId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.walletSummary });
    },
  });
}

export function useSendTransfer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: TransferRequest) => api.sendTransfer(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["balances"] });
    },
  });
}

export function useParseEsr() {
  return useMutation({
    mutationFn: (body: EsrParseRequest) => api.parseEsr(body),
  });
}

export function useApproveEsr() {
  return useMutation({
    mutationFn: (body: EsrApproveRequest) => api.approveEsr(body),
  });
}

export function useRejectEsr() {
  return useMutation({
    mutationFn: (body: EsrRejectRequest) => api.rejectEsr(body),
  });
}

export function useSignRawTransaction() {
  return useMutation({
    mutationFn: (body: SignRawRequest) => api.signRawTransaction(body),
  });
}

export function useSetAutoLockSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: AutoLockSettings) => api.setAutoLockSettings(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.autoLockSettings });
    },
  });
}

export function useSetAppSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: AppSettings) => api.setAppSettings(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.appSettings });
    },
  });
}
