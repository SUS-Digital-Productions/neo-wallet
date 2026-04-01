// HTTP routes for the embedded mobile backend.
// Mirrors the .NET backend REST API so the React frontend works unchanged.

use axum::{
    extract::{Query, State},
    http::{header, HeaderMap, StatusCode},
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};

use super::state::AppState;

// ---------------------------------------------------------------------------
// Router
// ---------------------------------------------------------------------------

pub fn build_router(state: AppState) -> Router {
    Router::new()
        // Health (no auth)
        .route("/api/health", get(health))
        // Wallet (create/unlock are open, rest requires auth)
        .route("/api/wallet/create", post(wallet_create))
        .route("/api/wallet/unlock", post(wallet_unlock))
        .route("/api/wallet/lock", post(wallet_lock))
        .route("/api/wallet/summary", get(wallet_summary))
        .route("/api/wallet/export", get(wallet_export))
        .route("/api/wallet/import", post(wallet_import))
        // Accounts
        .route("/api/accounts", get(accounts_list))
        .route("/api/accounts/active", post(accounts_set_active))
        .route("/api/accounts/import", post(accounts_import))
        .route("/api/accounts/remove", post(accounts_remove))
        .route("/api/accounts/private-key", post(accounts_private_key))
        .route("/api/accounts/lookup", post(accounts_lookup))
        // Keys
        .route("/api/keys", get(keys_list).post(keys_add))
        .route("/api/keys/remove", post(keys_remove))
        // Networks
        .route("/api/networks", get(networks_list))
        .route("/api/networks/active", post(networks_set_active))
        // Balances
        .route("/api/balances", get(balances))
        // Transfers
        .route("/api/transfers", post(transfers))
        // Settings
        .route("/api/settings/autolock", get(settings_autolock_get).post(settings_autolock_set))
        .route("/api/settings/app", get(settings_app_get).post(settings_app_set))
        // ESR stubs
        .route("/api/esr/parse", post(esr_stub))
        .route("/api/esr/approve", post(esr_stub))
        .route("/api/esr/reject", post(esr_stub))
        .route("/api/esr/incoming", post(esr_stub))
        .route("/api/esr/sign-raw", post(esr_stub))
        .route("/api/esr/listener/status", get(esr_listener_status))
        .route("/api/esr/listener/connect", post(esr_stub))
        .route("/api/esr/listener/disconnect", post(esr_stub))
        .with_state(state)
}

// ---------------------------------------------------------------------------
// Auth helper
// ---------------------------------------------------------------------------

/// Paths that skip bearer-token auth.
const OPEN_PATHS: &[&str] = &[
    "/api/health",
    "/api/wallet/create",
    "/api/wallet/unlock",
    "/api/esr/incoming",
];

fn check_auth(state: &AppState, headers: &HeaderMap, path: &str) -> Result<(), StatusCode> {
    if OPEN_PATHS.iter().any(|p| p.eq_ignore_ascii_case(path)) {
        return Ok(());
    }
    let expected = state.token();
    let provided = headers
        .get(header::AUTHORIZATION)
        .and_then(|v| v.to_str().ok())
        .and_then(|v| v.strip_prefix("Bearer "));

    match provided {
        Some(t) if t == expected => Ok(()),
        _ => Err(StatusCode::UNAUTHORIZED),
    }
}

macro_rules! require_auth {
    ($state:expr, $headers:expr, $path:expr) => {
        if let Err(status) = check_auth(&$state, &$headers, $path) {
            return Err(status);
        }
    };
}

// ---------------------------------------------------------------------------
// Network definitions
// ---------------------------------------------------------------------------

#[derive(Clone, Serialize)]
struct NetworkDef {
    chain_id: &'static str,
    name: &'static str,
    symbol: &'static str,
    rpc: &'static str,
}

const NETWORKS: &[NetworkDef] = &[
    NetworkDef {
        chain_id: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
        name: "WAX",
        symbol: "WAX",
        rpc: "https://wax.greymass.com",
    },
    NetworkDef {
        chain_id: "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
        name: "EOS",
        symbol: "EOS",
        rpc: "https://eos.greymass.com",
    },
    NetworkDef {
        chain_id: "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11",
        name: "Telos",
        symbol: "TLOS",
        rpc: "https://telos.greymass.com",
    },
];

fn find_network(chain_id: &str) -> Option<&'static NetworkDef> {
    NETWORKS.iter().find(|n| n.chain_id == chain_id)
}

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

#[derive(Serialize)]
struct HealthResponse {
    status: &'static str,
    version: &'static str,
    #[serde(rename = "walletLoaded")]
    wallet_loaded: bool,
    #[serde(rename = "walletUnlocked")]
    wallet_unlocked: bool,
}

#[derive(Deserialize)]
struct PasswordBody {
    password: String,
}

#[derive(Serialize)]
struct UnlockResponse {
    unlocked: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    token: Option<String>,
}

#[derive(Serialize)]
struct NetworkDto {
    #[serde(rename = "chainId")]
    chain_id: String,
    name: String,
    symbol: String,
}

#[derive(Serialize)]
struct AccountDto {
    account: String,
    authority: String,
    #[serde(rename = "publicKey")]
    public_key: String,
    #[serde(rename = "chainId")]
    chain_id: String,
    #[serde(rename = "chainName")]
    chain_name: String,
}

#[derive(Serialize)]
struct WalletSummaryDto {
    #[serde(rename = "activeNetwork")]
    active_network: Option<NetworkDto>,
    #[serde(rename = "activeAccount")]
    active_account: Option<AccountDto>,
    #[serde(rename = "listenerStatus")]
    listener_status: String,
}

#[derive(Deserialize)]
struct SetActiveAccountBody {
    account: String,
    authority: String,
    #[serde(rename = "chainId")]
    chain_id: String,
}

#[derive(Deserialize)]
struct ImportAccountEntry {
    account: String,
    authority: String,
    #[serde(rename = "chainId")]
    chain_id: String,
}

#[derive(Deserialize)]
struct ImportAccountBody {
    #[serde(rename = "privateKey")]
    private_key: String,
    accounts: Vec<ImportAccountEntry>,
}

#[derive(Deserialize)]
struct RemoveAccountBody {
    account: String,
    authority: String,
    #[serde(rename = "chainId")]
    chain_id: String,
}

#[derive(Deserialize)]
struct GetPrivateKeyBody {
    account: String,
    authority: String,
}

#[derive(Serialize)]
struct GetPrivateKeyResponse {
    #[serde(rename = "privateKey")]
    private_key: String,
}

#[derive(Serialize)]
struct KeyDto {
    #[serde(rename = "publicKey")]
    public_key: String,
    label: String,
    #[serde(rename = "accountCount")]
    account_count: usize,
}

#[derive(Deserialize)]
struct AddKeyBody {
    #[serde(rename = "privateKey")]
    private_key: String,
    label: String,
}

#[derive(Deserialize)]
struct RemoveKeyBody {
    #[serde(rename = "publicKey")]
    public_key: String,
}

#[derive(Deserialize)]
struct SetActiveNetworkBody {
    #[serde(rename = "chainId")]
    chain_id: String,
}

#[derive(Deserialize)]
struct BalanceQuery {
    account: Option<String>,
    #[serde(rename = "chainId")]
    chain_id: Option<String>,
}

#[derive(Serialize)]
struct BalanceEntry {
    symbol: String,
    amount: String,
    #[serde(rename = "numericAmount")]
    numeric_amount: f64,
}

#[derive(Serialize)]
struct AutoLockSettings {
    #[serde(rename = "timeoutMinutes")]
    timeout_minutes: u32,
}

#[derive(Deserialize)]
struct AutoLockBody {
    #[serde(rename = "timeoutMinutes")]
    timeout_minutes: u32,
}

#[derive(Serialize)]
struct AppSettingsDto {
    #[serde(rename = "startAtLogin")]
    start_at_login: bool,
    #[serde(rename = "minimizeToTray")]
    minimize_to_tray: bool,
}

#[derive(Serialize)]
struct EsrListenerStatusDto {
    status: String,
    #[serde(rename = "linkId")]
    link_id: String,
    #[serde(rename = "requestPublicKey")]
    request_public_key: Option<String>,
    #[serde(rename = "sessionCount")]
    session_count: u32,
}

#[derive(Deserialize)]
struct ImportWalletBody {
    password: String,
    #[serde(rename = "fileBase64")]
    file_base64: String,
}

#[derive(Deserialize)]
struct LookupAccountsBody {
    #[serde(rename = "privateKey")]
    private_key: String,
    #[serde(rename = "chainIds")]
    chain_ids: Option<Vec<String>>,
}

#[derive(Serialize)]
struct LookupAccountEntry {
    account: String,
    authority: String,
}

#[derive(Serialize)]
struct LookupChainResult {
    #[serde(rename = "chainId")]
    chain_id: String,
    name: String,
    symbol: String,
    accounts: Vec<LookupAccountEntry>,
}

#[derive(Serialize)]
struct LookupAccountsResponse {
    #[serde(rename = "publicKey")]
    public_key: String,
    chains: Vec<LookupChainResult>,
}

#[derive(Deserialize)]
struct TransferBody {
    #[serde(rename = "chainId")]
    chain_id: String,
    from: String,
    authority: String,
    to: String,
    quantity: String,
    memo: Option<String>,
}

#[derive(Serialize)]
struct TransferResponse {
    #[serde(rename = "transactionId")]
    transaction_id: String,
    broadcast: bool,
}

// ---------------------------------------------------------------------------
// Handlers
// ---------------------------------------------------------------------------

async fn health(State(state): State<AppState>) -> Json<HealthResponse> {
    Json(HealthResponse {
        status: "ok",
        version: env!("CARGO_PKG_VERSION"),
        wallet_loaded: state.wallet_file_exists(),
        wallet_unlocked: state.wallet_unlocked(),
    })
}

async fn wallet_create(
    State(state): State<AppState>,
    Json(body): Json<PasswordBody>,
) -> Result<Json<UnlockResponse>, StatusCode> {
    if body.password.len() < 8 {
        return Err(StatusCode::BAD_REQUEST);
    }
    state.create_wallet(&body.password).map_err(|e| {
        eprintln!("[mobile-backend] wallet create error: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    Ok(Json(UnlockResponse {
        unlocked: true,
        token: Some(state.token()),
    }))
}

async fn wallet_unlock(
    State(state): State<AppState>,
    Json(body): Json<PasswordBody>,
) -> Result<Json<UnlockResponse>, StatusCode> {
    let ok = state.unlock(&body.password).map_err(|e| {
        eprintln!("[mobile-backend] wallet unlock error: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    if ok {
        Ok(Json(UnlockResponse {
            unlocked: true,
            token: Some(state.token()),
        }))
    } else {
        Ok(Json(UnlockResponse {
            unlocked: false,
            token: None,
        }))
    }
}

async fn wallet_lock(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/wallet/lock");
    state.lock();
    Ok(StatusCode::OK)
}

async fn wallet_summary(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<WalletSummaryDto>, StatusCode> {
    require_auth!(state, headers, "/api/wallet/summary");
    let inner = state.inner.lock().unwrap();
    let active_network = find_network(&inner.active_chain_id).map(|n| NetworkDto {
        chain_id: n.chain_id.to_string(),
        name: n.name.to_string(),
        symbol: n.symbol.to_string(),
    });
    let active_account = inner
        .active_account
        .as_ref()
        .and_then(|acct| {
            let auth = inner.active_authority.as_deref().unwrap_or("active");
            inner.wallet_data.as_ref().and_then(|wd| {
                wd.accounts.iter().find(|a| {
                    a.account == *acct
                        && a.authority == auth
                        && a.chain_id == inner.active_chain_id
                })
            }).map(|a| {
                let chain_name = find_network(&a.chain_id)
                    .map(|n| n.name.to_string())
                    .unwrap_or_default();
                AccountDto {
                    account: a.account.clone(),
                    authority: a.authority.clone(),
                    public_key: a.public_key.clone(),
                    chain_id: a.chain_id.clone(),
                    chain_name,
                }
            })
        });
    Ok(Json(WalletSummaryDto {
        active_network,
        active_account,
        listener_status: "disconnected".to_string(),
    }))
}

async fn wallet_export(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Vec<u8>, StatusCode> {
    require_auth!(state, headers, "/api/wallet/export");
    let inner = state.inner.lock().unwrap();
    std::fs::read(&inner.wallet_path).map_err(|_| StatusCode::NOT_FOUND)
}

async fn wallet_import(
    State(state): State<AppState>,
    Json(body): Json<ImportWalletBody>,
) -> Result<Json<UnlockResponse>, StatusCode> {
    use base64::Engine;
    let raw = base64::engine::general_purpose::STANDARD
        .decode(&body.file_base64)
        .map_err(|_| StatusCode::BAD_REQUEST)?;

    // Verify the imported file can be decrypted with the provided password
    let file: super::crypto::WalletFile =
        serde_json::from_slice(&raw).map_err(|_| StatusCode::BAD_REQUEST)?;
    let _data = file
        .decrypt(&body.password)
        .map_err(|_| StatusCode::BAD_REQUEST)?;

    // Write the raw file and then unlock
    {
        let inner = state.inner.lock().unwrap();
        std::fs::write(&inner.wallet_path, &raw)
            .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    }
    state.lock();

    let ok = state.unlock(&body.password).map_err(|e| {
        eprintln!("[mobile-backend] wallet import unlock: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    if ok {
        Ok(Json(UnlockResponse {
            unlocked: true,
            token: Some(state.token()),
        }))
    } else {
        Err(StatusCode::BAD_REQUEST)
    }
}

// -- Accounts --

async fn accounts_list(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<Vec<AccountDto>>, StatusCode> {
    require_auth!(state, headers, "/api/accounts");
    let inner = state.inner.lock().unwrap();
    let data = inner.wallet_data.as_ref().ok_or(StatusCode::FORBIDDEN)?;
    let list = data
        .accounts
        .iter()
        .map(|a| {
            let chain_name = find_network(&a.chain_id)
                .map(|n| n.name.to_string())
                .unwrap_or_default();
            AccountDto {
                account: a.account.clone(),
                authority: a.authority.clone(),
                public_key: a.public_key.clone(),
                chain_id: a.chain_id.clone(),
                chain_name,
            }
        })
        .collect();
    Ok(Json(list))
}

async fn accounts_set_active(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<SetActiveAccountBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/accounts/active");
    let mut inner = state.inner.lock().unwrap();
    inner.active_account = Some(body.account);
    inner.active_authority = Some(body.authority);
    inner.active_chain_id = body.chain_id;
    Ok(StatusCode::OK)
}

async fn accounts_import(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<ImportAccountBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/accounts/import");

    // For now we store the private key as-is (WIF validation happens in the
    // .NET library on desktop — on mobile we trust the key format).
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        for entry in &body.accounts {
            // Avoid duplicates
            let exists = data.accounts.iter().any(|a| {
                a.account == entry.account
                    && a.authority == entry.authority
                    && a.chain_id == entry.chain_id
            });
            if !exists {
                data.accounts.push(super::crypto::WalletAccount {
                    account: entry.account.clone(),
                    authority: entry.authority.clone(),
                    private_key_wif: body.private_key.clone(),
                    public_key: String::new(), // Will be populated when we add secp256k1
                    chain_id: entry.chain_id.clone(),
                });
            }
        }
    }
    state.save().map_err(|e| {
        eprintln!("[mobile-backend] save after import: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    Ok(StatusCode::OK)
}

async fn accounts_remove(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<RemoveAccountBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/accounts/remove");
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        data.accounts.retain(|a| {
            !(a.account == body.account
                && a.authority == body.authority
                && a.chain_id == body.chain_id)
        });
    }
    state.save().map_err(|e| {
        eprintln!("[mobile-backend] save after remove: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    Ok(StatusCode::OK)
}

async fn accounts_private_key(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<GetPrivateKeyBody>,
) -> Result<Json<GetPrivateKeyResponse>, StatusCode> {
    require_auth!(state, headers, "/api/accounts/private-key");
    let inner = state.inner.lock().unwrap();
    let data = inner.wallet_data.as_ref().ok_or(StatusCode::FORBIDDEN)?;
    let acct = data
        .accounts
        .iter()
        .find(|a| a.account == body.account && a.authority == body.authority)
        .ok_or(StatusCode::NOT_FOUND)?;
    Ok(Json(GetPrivateKeyResponse {
        private_key: acct.private_key_wif.clone(),
    }))
}

async fn accounts_lookup(
    State(_state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<LookupAccountsBody>,
) -> Result<Json<LookupAccountsResponse>, StatusCode> {
    require_auth!(_state, headers, "/api/accounts/lookup");

    // Lookup accounts on each chain via the Light API.
    // For now, return empty chains — full lookup requires secp256k1 key derivation
    // and HTTP calls to the Light API which will be added later.
    let chain_ids: Vec<&str> = match &body.chain_ids {
        Some(ids) => ids.iter().map(|s| s.as_str()).collect(),
        None => NETWORKS.iter().map(|n| n.chain_id).collect(),
    };

    let chains = chain_ids
        .iter()
        .filter_map(|cid| {
            find_network(cid).map(|n| LookupChainResult {
                chain_id: n.chain_id.to_string(),
                name: n.name.to_string(),
                symbol: n.symbol.to_string(),
                accounts: vec![],
            })
        })
        .collect();

    Ok(Json(LookupAccountsResponse {
        public_key: String::new(), // TODO: derive from private key
        chains,
    }))
}

// -- Keys --

async fn keys_list(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<Vec<KeyDto>>, StatusCode> {
    require_auth!(state, headers, "/api/keys");
    let inner = state.inner.lock().unwrap();
    let data = inner.wallet_data.as_ref().ok_or(StatusCode::FORBIDDEN)?;
    let list = data
        .keys
        .iter()
        .map(|k| {
            let account_count = data
                .accounts
                .iter()
                .filter(|a| a.public_key == k.public_key)
                .count();
            KeyDto {
                public_key: k.public_key.clone(),
                label: k.label.clone(),
                account_count,
            }
        })
        .collect();
    Ok(Json(list))
}

async fn keys_add(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<AddKeyBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/keys");
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        data.keys.push(super::crypto::WalletKey {
            label: body.label,
            private_key_wif: body.private_key,
            public_key: String::new(), // TODO: derive from private key
        });
    }
    state.save().map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(StatusCode::OK)
}

async fn keys_remove(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<RemoveKeyBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/keys/remove");
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        data.keys.retain(|k| k.public_key != body.public_key);
    }
    state.save().map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    Ok(StatusCode::OK)
}

// -- Networks --

async fn networks_list(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<Vec<NetworkDto>>, StatusCode> {
    require_auth!(state, headers, "/api/networks");
    let list = NETWORKS
        .iter()
        .map(|n| NetworkDto {
            chain_id: n.chain_id.to_string(),
            name: n.name.to_string(),
            symbol: n.symbol.to_string(),
        })
        .collect();
    Ok(Json(list))
}

async fn networks_set_active(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<SetActiveNetworkBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/networks/active");
    let mut inner = state.inner.lock().unwrap();
    inner.active_chain_id = body.chain_id;
    Ok(StatusCode::OK)
}

// -- Balances --

async fn balances(
    State(state): State<AppState>,
    headers: HeaderMap,
    Query(_params): Query<BalanceQuery>,
) -> Result<Json<Vec<BalanceEntry>>, StatusCode> {
    require_auth!(state, headers, "/api/balances");
    // TODO: implement on-chain balance lookups via reqwest to the RPC endpoint.
    // For now return an empty list so the UI renders without errors.
    Ok(Json(vec![]))
}

// -- Transfers --

async fn transfers(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(_body): Json<TransferBody>,
) -> Result<Json<TransferResponse>, StatusCode> {
    require_auth!(state, headers, "/api/transfers");
    // TODO: implement transaction signing and broadcast.
    Err(StatusCode::NOT_IMPLEMENTED)
}

// -- Settings --

async fn settings_autolock_get(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<AutoLockSettings>, StatusCode> {
    require_auth!(state, headers, "/api/settings/autolock");
    Ok(Json(AutoLockSettings {
        timeout_minutes: 15,
    }))
}

async fn settings_autolock_set(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(_body): Json<AutoLockBody>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/settings/autolock");
    // TODO: persist auto-lock settings.
    Ok(StatusCode::OK)
}

async fn settings_app_get(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<AppSettingsDto>, StatusCode> {
    require_auth!(state, headers, "/api/settings/app");
    Ok(Json(AppSettingsDto {
        start_at_login: false,
        minimize_to_tray: false,
    }))
}

async fn settings_app_set(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(_body): Json<serde_json::Value>,
) -> Result<StatusCode, StatusCode> {
    require_auth!(state, headers, "/api/settings/app");
    Ok(StatusCode::OK)
}

// -- ESR stubs --

async fn esr_stub() -> StatusCode {
    // ESR signing/session management is not yet implemented on mobile.
    StatusCode::NOT_IMPLEMENTED
}

async fn esr_listener_status(
    State(state): State<AppState>,
    headers: HeaderMap,
) -> Result<Json<EsrListenerStatusDto>, StatusCode> {
    require_auth!(state, headers, "/api/esr/listener/status");
    Ok(Json(EsrListenerStatusDto {
        status: "disconnected".to_string(),
        link_id: String::new(),
        request_public_key: None,
        session_count: 0,
    }))
}
