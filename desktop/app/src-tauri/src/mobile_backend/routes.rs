// HTTP routes for the embedded mobile backend.
// Mirrors the .NET backend REST API so the React frontend works unchanged.

use axum::{
    extract::{Query, State},
    http::{header, HeaderMap, Method, StatusCode},
    routing::{get, post},
    Json, Router,
};
use axum::extract::ws::{Message, WebSocket, WebSocketUpgrade};
use axum::response::IntoResponse;
use tower_http::cors::{Any, CorsLayer};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

use super::chain::AsyncChainApi;
use super::state::AppState;
use eosio_signer::PrivateKey as EosioPrivateKey;

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
        .route("/api/wallet/import-anchor", post(wallet_import_anchor))
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
        // ESR WebSocket — keep-alive stub so the frontend doesn't reconnect-loop.
        .route("/api/esr/ws", get(esr_ws_handler))
        // Generic action signing (used by send page + every dedicated form)
        .route("/api/actions/sign", post(actions_sign))
        // Read-only chain queries
        .route("/api/chain/account", get(chain_account))
        .route("/api/chain/currency-balance", get(chain_currency_balance))
        .route("/api/chain/table-rows", post(chain_table_rows))
        .layer(
            CorsLayer::new()
                .allow_origin(Any)
                .allow_methods([Method::GET, Method::POST, Method::OPTIONS])
                .allow_headers([header::CONTENT_TYPE, header::AUTHORIZATION]),
        )
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
    // The WebSocket route does its own token-via-query check.
    "/api/esr/ws",
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
    /// Token contract for the native token.
    token_contract: &'static str,
}

const NETWORKS: &[NetworkDef] = &[
    NetworkDef {
        chain_id: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4",
        name: "WAX",
        symbol: "WAX",
        rpc: "https://wax.greymass.com",
        token_contract: "eosio.token",
    },
    NetworkDef {
        chain_id: "aca376f206b8fc25a6ed44dbdc66547c36c6c33e3a119ffbeaef943642f0e906",
        name: "EOS",
        symbol: "EOS",
        rpc: "https://eos.greymass.com",
        token_contract: "eosio.token",
    },
    NetworkDef {
        chain_id: "4667b205c6838ef70ff7988f6e8257e8be0e1284a2f59699054a018f743b1d11",
        name: "Telos",
        symbol: "TLOS",
        rpc: "https://telos.greymass.com",
        token_contract: "eosio.token",
    },
];

/// Embedded eosio.token ABI for transfer action encoding.
const TOKEN_ABI: &str = r#"{
    "version": "eosio::abi/1.2",
    "structs": [{
        "name": "transfer",
        "base": "",
        "fields": [
            {"name": "from", "type": "name"},
            {"name": "to", "type": "name"},
            {"name": "quantity", "type": "asset"},
            {"name": "memo", "type": "string"}
        ]
    }],
    "actions": [{"name": "transfer", "type": "transfer", "ricardian_contract": ""}],
    "tables": [],
    "types": [],
    "variants": []
}"#;

/// Derive the legacy EOS public key string from a WIF private key.
fn derive_public_key(wif: &str) -> Result<String, String> {
    let key = EosioPrivateKey::from_wif(wif)
        .map_err(|e| format!("{e}"))?;
    Ok(key.public_key().to_legacy_string())
}

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

    let public_key = derive_public_key(&body.private_key)
        .map_err(|e| {
            eprintln!("[mobile-backend] invalid WIF key: {e}");
            StatusCode::BAD_REQUEST
        })?;
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
                    public_key: public_key.clone(),
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
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<LookupAccountsBody>,
) -> Result<Json<LookupAccountsResponse>, StatusCode> {
    require_auth!(state, headers, "/api/accounts/lookup");

    let public_key = derive_public_key(&body.private_key)
        .map_err(|e| {
            eprintln!("[mobile-backend] lookup: invalid WIF: {e}");
            StatusCode::BAD_REQUEST
        })?;

    let chain_ids: Vec<&str> = match &body.chain_ids {
        Some(ids) => ids.iter().map(|s| s.as_str()).collect(),
        None => NETWORKS.iter().map(|n| n.chain_id).collect(),
    };

    let mut chains = Vec::new();
    for cid in &chain_ids {
        if let Some(net) = find_network(cid) {
            let api = AsyncChainApi::new(net.rpc);
            let account_names = api.get_key_accounts(&public_key).await.unwrap_or_default();
            let accounts = account_names
                .into_iter()
                .map(|name| LookupAccountEntry {
                    account: name,
                    authority: "active".to_string(),
                })
                .collect();
            chains.push(LookupChainResult {
                chain_id: net.chain_id.to_string(),
                name: net.name.to_string(),
                symbol: net.symbol.to_string(),
                accounts,
            });
        }
    }

    Ok(Json(LookupAccountsResponse {
        public_key,
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
    let public_key = derive_public_key(&body.private_key)
        .map_err(|e| {
            eprintln!("[mobile-backend] keys_add: invalid WIF: {e}");
            StatusCode::BAD_REQUEST
        })?;
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        data.keys.push(super::crypto::WalletKey {
            label: body.label,
            private_key_wif: body.private_key,
            public_key,
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
    Query(params): Query<BalanceQuery>,
) -> Result<Json<Vec<BalanceEntry>>, StatusCode> {
    require_auth!(state, headers, "/api/balances");

    let (account, chain_id) = {
        let inner = state.inner.lock().unwrap();
        let acct = params
            .account
            .or_else(|| inner.active_account.clone())
            .ok_or(StatusCode::BAD_REQUEST)?;
        let cid = params
            .chain_id
            .unwrap_or_else(|| inner.active_chain_id.clone());
        (acct, cid)
    };

    let net = find_network(&chain_id).ok_or(StatusCode::BAD_REQUEST)?;
    let api = AsyncChainApi::new(net.rpc);

    let balances_raw = api
        .get_currency_balance(net.token_contract, &account, net.symbol)
        .await
        .unwrap_or_default();

    let entries: Vec<BalanceEntry> = balances_raw
        .iter()
        .filter_map(|s| {
            // Format: "123.45678900 WAX"
            let parts: Vec<&str> = s.trim().splitn(2, ' ').collect();
            if parts.len() != 2 {
                return None;
            }
            let numeric: f64 = parts[0].parse().unwrap_or(0.0);
            Some(BalanceEntry {
                symbol: parts[1].to_string(),
                amount: s.clone(),
                numeric_amount: numeric,
            })
        })
        .collect();

    Ok(Json(entries))
}

// -- Transfers --

async fn transfers(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<TransferBody>,
) -> Result<Json<TransferResponse>, StatusCode> {
    require_auth!(state, headers, "/api/transfers");

    let net = find_network(&body.chain_id).ok_or(StatusCode::BAD_REQUEST)?;

    // Get the private key for the sender account
    let private_key_wif = {
        let inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_ref().ok_or(StatusCode::FORBIDDEN)?;
        let acct = data
            .accounts
            .iter()
            .find(|a| a.account == body.from && a.authority == body.authority && a.chain_id == body.chain_id)
            .ok_or(StatusCode::NOT_FOUND)?;
        acct.private_key_wif.clone()
    };

    let key = EosioPrivateKey::from_wif(&private_key_wif).map_err(|e| {
        eprintln!("[mobile-backend] transfer: invalid key: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;

    // Encode action data using the embedded eosio.token ABI
    let codec = eosio_abi::AbiCodec::from_json(TOKEN_ABI).map_err(|e| {
        eprintln!("[mobile-backend] transfer: ABI parse: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let action_json = serde_json::json!({
        "from": body.from,
        "to": body.to,
        "quantity": body.quantity,
        "memo": body.memo.as_deref().unwrap_or("")
    });
    let encoded_data = codec.encode_action("transfer", &action_json).map_err(|e| {
        eprintln!("[mobile-backend] transfer: ABI encode: {e}");
        StatusCode::BAD_REQUEST
    })?;

    // Build action
    let mut action = eosio_signer::Action::new(
        net.token_contract,
        "transfer",
        vec![eosio_signer::PermissionLevel::new(&body.from, &body.authority)],
    );
    action.set_data_bytes(encoded_data);

    // Get chain info and build transaction
    let api = AsyncChainApi::new(net.rpc);
    let info = api.get_info().await.map_err(|e| {
        eprintln!("[mobile-backend] transfer: get_info: {e}");
        StatusCode::BAD_GATEWAY
    })?;
    let block = api.get_block_info(info.last_irreversible_block_num).await.map_err(|e| {
        eprintln!("[mobile-backend] transfer: get_block_info: {e}");
        StatusCode::BAD_GATEWAY
    })?;

    let head_time = super::chain::parse_eosio_time(&info.head_block_time).map_err(|e| {
        eprintln!("[mobile-backend] transfer: parse time: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let expiration = head_time + 120;

    let id_bytes = hex::decode(&block.id).map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    let ref_block_num = (block.block_num & 0xFFFF) as u16;
    let ref_block_prefix = if id_bytes.len() >= 12 {
        u32::from_le_bytes([id_bytes[8], id_bytes[9], id_bytes[10], id_bytes[11]])
    } else {
        0
    };

    let header = eosio_signer::transaction::TransactionHeader {
        expiration,
        ref_block_num,
        ref_block_prefix,
        max_net_usage_words: 0,
        max_cpu_usage_ms: 0,
        delay_sec: 0,
    };
    let tx = eosio_signer::transaction::Transaction::new(header, vec![action]);

    // Sign
    let packed = eosio_signer::sign_transaction(&tx, &info.chain_id, &key).map_err(|e| {
        eprintln!("[mobile-backend] transfer: sign: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;

    // Broadcast
    let result = api.push_transaction(&packed).await.map_err(|e| {
        eprintln!("[mobile-backend] transfer: push: {e}");
        StatusCode::BAD_GATEWAY
    })?;

    Ok(Json(TransferResponse {
        transaction_id: result.transaction_id,
        broadcast: true,
    }))
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

// ---------------------------------------------------------------------------
// ESR WebSocket — stub keep-alive
// ---------------------------------------------------------------------------
//
// The frontend opens `ws://.../api/esr/ws?token=...` to receive signing-request
// pushes. Mobile doesn't implement the Anchor link relay yet, so we simply
// validate the token and hold the connection open so the frontend doesn't
// reconnect-loop every 5 seconds.

#[derive(Deserialize)]
struct WsAuthQuery {
    token: Option<String>,
}

async fn esr_ws_handler(
    State(state): State<AppState>,
    Query(q): Query<WsAuthQuery>,
    ws: WebSocketUpgrade,
) -> impl IntoResponse {
    let expected = state.token();
    let provided = q.token.unwrap_or_default();
    if provided != expected {
        return StatusCode::UNAUTHORIZED.into_response();
    }
    ws.on_upgrade(handle_esr_socket)
}

async fn handle_esr_socket(mut socket: WebSocket) {
    // Send an initial "status_changed: disconnected" so the UI knows the
    // listener is alive but no Anchor link is wired up.
    let initial =
        r#"{"type":"status_changed","status":"disconnected"}"#.to_string();
    let _ = socket.send(Message::Text(initial.into())).await;

    // Keep the socket open; respond to pings; drop on first error.
    while let Some(Ok(msg)) = socket.recv().await {
        match msg {
            Message::Ping(p) => {
                if socket.send(Message::Pong(p)).await.is_err() {
                    break;
                }
            }
            Message::Close(_) => break,
            _ => {}
        }
    }
}

// ---------------------------------------------------------------------------
// Anchor wallet import
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct ImportAnchorBody {
    password: String,
    #[serde(rename = "fileBase64")]
    file_base64: String,
}

#[derive(Serialize)]
struct ImportAnchorResponse {
    #[serde(rename = "importedKeys")]
    imported_keys: usize,
    #[serde(rename = "publicKeys")]
    public_keys: Vec<String>,
    format: String,
}

async fn wallet_import_anchor(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<ImportAnchorBody>,
) -> Result<Json<ImportAnchorResponse>, StatusCode> {
    require_auth!(state, headers, "/api/wallet/import-anchor");

    use base64::Engine;
    let raw = base64::engine::general_purpose::STANDARD
        .decode(&body.file_base64)
        .map_err(|_| StatusCode::BAD_REQUEST)?;

    let result = match super::anchor::try_import(&raw, &body.password) {
        Some(r) => r,
        None => return Err(StatusCode::BAD_REQUEST),
    };

    let mut public_keys = Vec::with_capacity(result.keys_wif.len());
    {
        let mut inner = state.inner.lock().unwrap();
        let data = inner.wallet_data.as_mut().ok_or(StatusCode::FORBIDDEN)?;
        for wif in &result.keys_wif {
            let pk = match derive_public_key(wif) {
                Ok(p) => p,
                Err(_) => continue,
            };
            // Skip duplicates by public key.
            if data.keys.iter().any(|k| k.public_key == pk) {
                continue;
            }
            data.keys.push(super::crypto::WalletKey {
                label: format!("Imported from Anchor ({})", result.format),
                private_key_wif: wif.clone(),
                public_key: pk.clone(),
            });
            public_keys.push(pk);
        }
    }
    state.save().map_err(|e| {
        eprintln!("[mobile-backend] anchor import save: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;

    Ok(Json(ImportAnchorResponse {
        imported_keys: public_keys.len(),
        public_keys,
        format: result.format,
    }))
}

// ---------------------------------------------------------------------------
// Generic action signing
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct SignActionAuth {
    actor: String,
    permission: String,
}

#[derive(Deserialize)]
struct SignAction {
    account: String,
    name: String,
    #[serde(default)]
    data: serde_json::Value,
    #[serde(default)]
    authorization: Option<Vec<SignActionAuth>>,
}

#[derive(Deserialize)]
struct SignActionsRequest {
    #[serde(rename = "chainId", default)]
    chain_id: Option<String>,
    actions: Vec<SignAction>,
    #[serde(default)]
    broadcast: bool,
}

#[derive(Serialize)]
struct SignActionsResponse {
    #[serde(rename = "transactionId")]
    transaction_id: String,
    broadcast: bool,
}

async fn actions_sign(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<SignActionsRequest>,
) -> Result<Json<SignActionsResponse>, StatusCode> {
    require_auth!(state, headers, "/api/actions/sign");

    if req.actions.is_empty() {
        return Err(StatusCode::BAD_REQUEST);
    }

    // Resolve active account + private key.
    let (active_account, active_authority, active_chain_id, private_key_wif) = {
        let inner = state.inner.lock().unwrap();
        let acct = inner.active_account.clone().ok_or(StatusCode::BAD_REQUEST)?;
        let auth = inner
            .active_authority
            .clone()
            .unwrap_or_else(|| "active".to_string());
        let cid = inner.active_chain_id.clone();
        let data = inner.wallet_data.as_ref().ok_or(StatusCode::FORBIDDEN)?;
        let entry = data
            .accounts
            .iter()
            .find(|a| a.account == acct && a.authority == auth && a.chain_id == cid)
            .ok_or(StatusCode::BAD_REQUEST)?;
        (acct, auth, cid, entry.private_key_wif.clone())
    };

    let chain_id = req.chain_id.clone().unwrap_or(active_chain_id);
    let net = find_network(&chain_id).ok_or(StatusCode::BAD_REQUEST)?;
    let api = AsyncChainApi::new(net.rpc);

    // Per-tx ABI cache.
    let mut abi_cache: HashMap<String, eosio_abi::AbiCodec> = HashMap::new();

    // Encode each action.
    let mut encoded_actions: Vec<eosio_signer::Action> = Vec::with_capacity(req.actions.len());
    for action in &req.actions {
        if action.account.is_empty() || action.name.is_empty() {
            return Err(StatusCode::BAD_REQUEST);
        }

        // Fetch & cache ABI for the contract.
        if !abi_cache.contains_key(&action.account) {
            let abi_resp = api.get_abi(&action.account).await.map_err(|e| {
                eprintln!("[mobile-backend] actions_sign get_abi {}: {e}", action.account);
                StatusCode::BAD_GATEWAY
            })?;
            let abi_value = abi_resp.get("abi").cloned().unwrap_or(serde_json::Value::Null);
            if abi_value.is_null() {
                eprintln!("[mobile-backend] no ABI for {}", action.account);
                return Err(StatusCode::BAD_GATEWAY);
            }
            let abi_json = serde_json::to_string(&abi_value).map_err(|e| {
                eprintln!("[mobile-backend] abi serialize: {e}");
                StatusCode::INTERNAL_SERVER_ERROR
            })?;
            let codec = eosio_abi::AbiCodec::from_json(&abi_json).map_err(|e| {
                eprintln!("[mobile-backend] abi parse: {e}");
                StatusCode::INTERNAL_SERVER_ERROR
            })?;
            abi_cache.insert(action.account.clone(), codec);
        }
        let codec = abi_cache.get(&action.account).expect("codec just inserted");

        let encoded = codec.encode_action(&action.name, &action.data).map_err(|e| {
            eprintln!("[mobile-backend] abi encode {}::{}: {e}", action.account, action.name);
            StatusCode::BAD_REQUEST
        })?;

        // Authorization defaults to active account.
        let auths: Vec<eosio_signer::PermissionLevel> = match &action.authorization {
            Some(list) if !list.is_empty() => list
                .iter()
                .map(|a| eosio_signer::PermissionLevel::new(&a.actor, &a.permission))
                .collect(),
            _ => vec![eosio_signer::PermissionLevel::new(
                &active_account,
                &active_authority,
            )],
        };

        let mut a = eosio_signer::Action::new(&action.account, &action.name, auths);
        a.set_data_bytes(encoded);
        encoded_actions.push(a);
    }

    // Build header.
    let info = api.get_info().await.map_err(|e| {
        eprintln!("[mobile-backend] actions_sign get_info: {e}");
        StatusCode::BAD_GATEWAY
    })?;
    let block = api
        .get_block_info(info.last_irreversible_block_num)
        .await
        .map_err(|e| {
            eprintln!("[mobile-backend] actions_sign get_block_info: {e}");
            StatusCode::BAD_GATEWAY
        })?;
    let head_time = super::chain::parse_eosio_time(&info.head_block_time)
        .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    let id_bytes = hex::decode(&block.id).map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;
    let ref_block_num = (block.block_num & 0xFFFF) as u16;
    let ref_block_prefix = if id_bytes.len() >= 12 {
        u32::from_le_bytes([id_bytes[8], id_bytes[9], id_bytes[10], id_bytes[11]])
    } else {
        0
    };
    let header = eosio_signer::transaction::TransactionHeader {
        expiration: head_time + 120,
        ref_block_num,
        ref_block_prefix,
        max_net_usage_words: 0,
        max_cpu_usage_ms: 0,
        delay_sec: 0,
    };
    let tx = eosio_signer::transaction::Transaction::new(header, encoded_actions);

    // Sign.
    let key = EosioPrivateKey::from_wif(&private_key_wif).map_err(|e| {
        eprintln!("[mobile-backend] actions_sign invalid key: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;
    let packed = eosio_signer::sign_transaction(&tx, &info.chain_id, &key).map_err(|e| {
        eprintln!("[mobile-backend] actions_sign sign: {e}");
        StatusCode::INTERNAL_SERVER_ERROR
    })?;

    if !req.broadcast {
        return Ok(Json(SignActionsResponse {
            transaction_id: "signed-not-broadcast".to_string(),
            broadcast: false,
        }));
    }

    let result = api.push_transaction(&packed).await.map_err(|e| {
        eprintln!("[mobile-backend] actions_sign push: {e}");
        StatusCode::BAD_GATEWAY
    })?;

    Ok(Json(SignActionsResponse {
        transaction_id: result.transaction_id,
        broadcast: true,
    }))
}

// ---------------------------------------------------------------------------
// Read-only chain queries
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct ChainAccountQuery {
    account: String,
    #[serde(rename = "chainId")]
    chain_id: String,
}

async fn chain_account(
    State(state): State<AppState>,
    headers: HeaderMap,
    Query(q): Query<ChainAccountQuery>,
) -> Result<Json<serde_json::Value>, StatusCode> {
    require_auth!(state, headers, "/api/chain/account");
    let net = find_network(&q.chain_id).ok_or(StatusCode::BAD_REQUEST)?;
    let api = AsyncChainApi::new(net.rpc);
    let raw = api.get_account(&q.account).await.map_err(|e| {
        eprintln!("[mobile-backend] chain_account: {e}");
        StatusCode::BAD_GATEWAY
    })?;
    Ok(Json(raw))
}

#[derive(Deserialize)]
struct CurrencyBalanceQuery {
    account: String,
    #[serde(rename = "chainId")]
    chain_id: String,
    contract: Option<String>,
    symbol: Option<String>,
}

async fn chain_currency_balance(
    State(state): State<AppState>,
    headers: HeaderMap,
    Query(q): Query<CurrencyBalanceQuery>,
) -> Result<Json<serde_json::Value>, StatusCode> {
    require_auth!(state, headers, "/api/chain/currency-balance");
    let net = find_network(&q.chain_id).ok_or(StatusCode::BAD_REQUEST)?;
    let api = AsyncChainApi::new(net.rpc);
    let contract = q.contract.as_deref().unwrap_or("eosio.token");
    let raw = api
        .get_currency_balance_raw(contract, &q.account, q.symbol.as_deref())
        .await
        .map_err(|e| {
            eprintln!("[mobile-backend] chain_currency_balance: {e}");
            StatusCode::BAD_GATEWAY
        })?;
    Ok(Json(raw))
}

async fn chain_table_rows(
    State(state): State<AppState>,
    headers: HeaderMap,
    Json(mut body): Json<serde_json::Value>,
) -> Result<Json<serde_json::Value>, StatusCode> {
    require_auth!(state, headers, "/api/chain/table-rows");

    let chain_id = body
        .get("chainId")
        .and_then(|v| v.as_str())
        .map(|s| s.to_string())
        .ok_or(StatusCode::BAD_REQUEST)?;
    let net = find_network(&chain_id).ok_or(StatusCode::BAD_REQUEST)?;

    // Strip chainId before forwarding.
    if let Some(obj) = body.as_object_mut() {
        obj.remove("chainId");
    }

    let api = AsyncChainApi::new(net.rpc);
    let raw = api.get_table_rows(&body).await.map_err(|e| {
        eprintln!("[mobile-backend] chain_table_rows: {e}");
        StatusCode::BAD_GATEWAY
    })?;
    Ok(Json(raw))
}
