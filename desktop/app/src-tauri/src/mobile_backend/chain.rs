// Async chain API client for the embedded mobile backend.
// Uses async reqwest (not the blocking variant from eosio-signer) to work
// properly inside an axum/tokio async context.

use eosio_signer::transaction::PackedTransaction;
use reqwest::Client;
use serde::Deserialize;

/// Minimal chain info from `/v1/chain/get_info`.
#[derive(Debug, Deserialize)]
pub struct ChainInfo {
    pub chain_id: String,
    pub last_irreversible_block_num: u64,
    pub head_block_time: String,
}

/// Block info from `/v1/chain/get_block_info`.
#[derive(Debug, Deserialize)]
pub struct BlockInfo {
    pub block_num: u64,
    pub id: String,
    pub ref_block_prefix: u64,
}

/// Response from `/v1/chain/push_transaction`.
#[derive(Debug, Deserialize)]
pub struct PushTransactionResponse {
    pub transaction_id: String,
}

/// Response object from Light API key lookup.
#[derive(Debug, Deserialize)]
pub struct LightApiKeyResponse {
    #[serde(default)]
    pub account_names: Vec<String>,
}

/// Async HTTP client for EOSIO chain APIs.
pub struct AsyncChainApi {
    client: Client,
    base_url: String,
}

impl AsyncChainApi {
    pub fn new(base_url: &str) -> Self {
        Self {
            client: Client::new(),
            base_url: base_url.trim_end_matches('/').to_string(),
        }
    }

    /// GET /v1/chain/get_info
    pub async fn get_info(&self) -> Result<ChainInfo, String> {
        let url = format!("{}/v1/chain/get_info", self.base_url);
        let resp = self
            .client
            .get(&url)
            .send()
            .await
            .map_err(|e| format!("get_info request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_info parse: {e}"))
    }

    /// POST /v1/chain/get_block_info
    pub async fn get_block_info(&self, block_num: u64) -> Result<BlockInfo, String> {
        let url = format!("{}/v1/chain/get_block_info", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(&serde_json::json!({ "block_num": block_num }))
            .send()
            .await
            .map_err(|e| format!("get_block_info request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_block_info parse: {e}"))
    }

    /// POST /v1/chain/push_transaction
    pub async fn push_transaction(
        &self,
        packed: &PackedTransaction,
    ) -> Result<PushTransactionResponse, String> {
        let url = format!("{}/v1/chain/push_transaction", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(packed)
            .send()
            .await
            .map_err(|e| format!("push_transaction request: {e}"))?;

        let text = resp.text().await.map_err(|e| format!("push_transaction read: {e}"))?;
        let value: serde_json::Value =
            serde_json::from_str(&text).map_err(|e| format!("push_transaction parse: {e}"))?;

        // Check for chain-level error
        if let Some(code) = value.get("code").and_then(|c| c.as_u64()) {
            if code >= 400 {
                let msg = value
                    .get("error")
                    .and_then(|e| e.get("details"))
                    .and_then(|d| d.as_array())
                    .map(|details| {
                        details
                            .iter()
                            .filter_map(|d| d.get("message").and_then(|m| m.as_str()))
                            .collect::<Vec<_>>()
                            .join("; ")
                    })
                    .unwrap_or_else(|| "unknown error".to_string());
                return Err(msg);
            }
        }

        serde_json::from_str::<PushTransactionResponse>(&text)
            .map_err(|e| format!("push_transaction deserialize: {e}"))
    }

    /// POST /v1/chain/get_currency_balance
    pub async fn get_currency_balance(
        &self,
        code: &str,
        account: &str,
        symbol: &str,
    ) -> Result<Vec<String>, String> {
        let url = format!("{}/v1/chain/get_currency_balance", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(&serde_json::json!({
                "code": code,
                "account": account,
                "symbol": symbol
            }))
            .send()
            .await
            .map_err(|e| format!("get_currency_balance request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_currency_balance parse: {e}"))
    }

    /// POST /v1/history/get_key_accounts — returns account names for a public key.
    /// Falls back to empty on chains that don't support this endpoint.
    pub async fn get_key_accounts(&self, public_key: &str) -> Result<Vec<String>, String> {
        let url = format!("{}/v1/history/get_key_accounts", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(&serde_json::json!({ "public_key": public_key }))
            .send()
            .await
            .map_err(|e| format!("get_key_accounts request: {e}"))?;

        let value: serde_json::Value = resp
            .json()
            .await
            .map_err(|e| format!("get_key_accounts parse: {e}"))?;

        // Response: { "account_names": ["acc1", "acc2"] }
        Ok(value
            .get("account_names")
            .and_then(|v| v.as_array())
            .map(|arr| {
                arr.iter()
                    .filter_map(|v| v.as_str().map(|s| s.to_string()))
                    .collect()
            })
            .unwrap_or_default())
    }

    /// POST /v1/chain/get_abi — fetch ABI for a contract.
    pub async fn get_abi(&self, account: &str) -> Result<serde_json::Value, String> {
        let url = format!("{}/v1/chain/get_abi", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(&serde_json::json!({ "account_name": account }))
            .send()
            .await
            .map_err(|e| format!("get_abi request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_abi parse: {e}"))
    }

    /// POST /v1/chain/get_account — full nodeos account JSON (resources, perms…).
    pub async fn get_account(&self, account: &str) -> Result<serde_json::Value, String> {
        let url = format!("{}/v1/chain/get_account", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(&serde_json::json!({ "account_name": account }))
            .send()
            .await
            .map_err(|e| format!("get_account request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_account parse: {e}"))
    }

    /// POST /v1/chain/get_table_rows — generic table query passthrough.
    pub async fn get_table_rows(
        &self,
        body: &serde_json::Value,
    ) -> Result<serde_json::Value, String> {
        let url = format!("{}/v1/chain/get_table_rows", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(body)
            .send()
            .await
            .map_err(|e| format!("get_table_rows request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_table_rows parse: {e}"))
    }

    /// POST /v1/chain/get_currency_balance with optional symbol.
    pub async fn get_currency_balance_raw(
        &self,
        contract: &str,
        account: &str,
        symbol: Option<&str>,
    ) -> Result<serde_json::Value, String> {
        let url = format!("{}/v1/chain/get_currency_balance", self.base_url);
        let mut body = serde_json::json!({
            "code": contract,
            "account": account,
        });
        if let Some(s) = symbol {
            body["symbol"] = serde_json::Value::String(s.to_string());
        }
        let resp = self
            .client
            .post(&url)
            .json(&body)
            .send()
            .await
            .map_err(|e| format!("get_currency_balance request: {e}"))?;
        resp.json()
            .await
            .map_err(|e| format!("get_currency_balance parse: {e}"))
    }
}

/// Parse EOSIO timestamp `"2024-01-01T00:00:00"` → Unix seconds (u32).
pub fn parse_eosio_time(s: &str) -> Result<u32, String> {
    let s = s.trim().trim_end_matches('Z');
    let parts: Vec<&str> = s.split('T').collect();
    if parts.len() != 2 {
        return Err(format!("invalid time: '{s}'"));
    }

    let date_parts: Vec<u32> = parts[0]
        .split('-')
        .filter_map(|p| p.parse().ok())
        .collect();
    let time_parts: Vec<u32> = parts[1]
        .split(':')
        .filter_map(|p| p.parse().ok())
        .collect();

    if date_parts.len() != 3 || time_parts.len() != 3 {
        return Err(format!("invalid time components: '{s}'"));
    }

    let (year, month, day) = (date_parts[0], date_parts[1], date_parts[2]);
    let (hour, min, sec) = (time_parts[0], time_parts[1], time_parts[2]);

    let mut days: u32 = 0;
    for y in 1970..year {
        days += if is_leap_year(y) { 366 } else { 365 };
    }
    let month_days = [0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    for m in 1..month {
        days += month_days[m as usize];
        if m == 2 && is_leap_year(year) {
            days += 1;
        }
    }
    days += day - 1;

    Ok(days * 86400 + hour * 3600 + min * 60 + sec)
}

fn is_leap_year(y: u32) -> bool {
    (y % 4 == 0 && y % 100 != 0) || (y % 400 == 0)
}
