use serde::Deserialize;

use crate::keys::PrivateKey;
use crate::signing::sign_transaction;
use crate::transaction::{
    Action, PackedTransaction, Transaction, TransactionHeader,
};
use crate::SignerError;

/// Response from `/v1/chain/get_info`.
#[derive(Debug, Clone, Deserialize)]
pub struct ChainInfo {
    pub server_version: String,
    pub chain_id: String,
    pub head_block_num: u64,
    pub last_irreversible_block_num: u64,
    pub last_irreversible_block_id: String,
    pub head_block_id: String,
    pub head_block_time: String,
    pub head_block_producer: String,
}

/// Response from `/v1/chain/get_block_info`.
#[derive(Debug, Clone, Deserialize)]
pub struct BlockInfo {
    pub block_num: u64,
    pub id: String,
    pub timestamp: String,
    pub ref_block_prefix: u64,
}

/// Response from `/v1/chain/push_transaction`.
#[derive(Debug, Clone, Deserialize)]
pub struct PushTransactionResponse {
    pub transaction_id: String,
    pub processed: serde_json::Value,
}

/// Response from `/v1/chain/get_table_rows`.
#[derive(Debug, Clone, Deserialize)]
pub struct TableRowsResponse {
    pub rows: Vec<serde_json::Value>,
    pub more: bool,
    #[serde(default)]
    pub next_key: String,
}

/// Response from `/v1/chain/get_account`.
#[derive(Debug, Clone, Deserialize)]
pub struct AccountInfo {
    pub account_name: String,
    pub head_block_num: u64,
    pub head_block_time: String,
    #[serde(default)]
    pub ram_quota: i64,
    #[serde(default)]
    pub ram_usage: i64,
    #[serde(default)]
    pub net_limit: ResourceLimit,
    #[serde(default)]
    pub cpu_limit: ResourceLimit,
    #[serde(default)]
    pub permissions: Vec<serde_json::Value>,
}

#[derive(Debug, Clone, Default, Deserialize)]
pub struct ResourceLimit {
    #[serde(default)]
    pub used: i64,
    #[serde(default)]
    pub available: i64,
    #[serde(default)]
    pub max: i64,
    #[serde(default)]
    pub current_used: Option<i64>,
}

/// Blocking HTTP client for EOSIO chain API.
pub struct ChainApi {
    base_url: String,
    client: reqwest::blocking::Client,
}

impl ChainApi {
    /// Create a new API client.
    ///
    /// `base_url` should be like `"https://testnet.waxsweden.org"` (no trailing slash).
    pub fn new(base_url: &str) -> Self {
        Self {
            base_url: base_url.trim_end_matches('/').to_string(),
            client: reqwest::blocking::Client::new(),
        }
    }

    /// GET `/v1/chain/get_info`
    pub fn get_info(&self) -> Result<ChainInfo, SignerError> {
        let url = format!("{}/v1/chain/get_info", self.base_url);
        let resp = self
            .client
            .get(&url)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let info: ChainInfo = resp
            .json()
            .map_err(|e| SignerError::NetworkError(format!("parse get_info: {}", e)))?;
        Ok(info)
    }

    /// POST `/v1/chain/get_block_info`
    pub fn get_block_info(&self, block_num: u64) -> Result<BlockInfo, SignerError> {
        let url = format!("{}/v1/chain/get_block_info", self.base_url);
        let body = serde_json::json!({ "block_num": block_num });
        let resp = self
            .client
            .post(&url)
            .json(&body)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let block: BlockInfo = resp
            .json()
            .map_err(|e| SignerError::NetworkError(format!("parse get_block_info: {}", e)))?;
        Ok(block)
    }

    /// POST `/v1/chain/push_transaction`
    pub fn push_transaction(
        &self,
        packed: &PackedTransaction,
    ) -> Result<PushTransactionResponse, SignerError> {
        let url = format!("{}/v1/chain/push_transaction", self.base_url);
        let resp = self
            .client
            .post(&url)
            .json(packed)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let text = resp
            .text()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        // Check for API errors
        let value: serde_json::Value = serde_json::from_str(&text)
            .map_err(|e| SignerError::NetworkError(format!("parse push_transaction: {}", e)))?;

        if let Some(code) = value.get("code").and_then(|c| c.as_u64()) {
            if code >= 400 {
                let msg = value
                    .get("error")
                    .and_then(|e| e.get("details"))
                    .and_then(|d| d.as_array())
                    .map(|details| {
                        details.iter()
                            .filter_map(|d| d.get("message").and_then(|m| m.as_str()))
                            .collect::<Vec<_>>()
                            .join("; ")
                    })
                    .unwrap_or_else(|| "unknown error".to_string());
                let error_name = value
                    .get("error")
                    .and_then(|e| e.get("what"))
                    .and_then(|w| w.as_str())
                    .unwrap_or("");
                return Err(SignerError::ChainError(format!(
                    "code {}: {} [{}]", code, msg, error_name
                )));
            }
        }

        let result: PushTransactionResponse = serde_json::from_str(&text)
            .map_err(|e| SignerError::NetworkError(format!("parse push_transaction: {}", e)))?;
        Ok(result)
    }

    /// POST `/v1/chain/get_table_rows`
    pub fn get_table_rows(
        &self,
        code: &str,
        scope: &str,
        table: &str,
        limit: u32,
    ) -> Result<TableRowsResponse, SignerError> {
        let url = format!("{}/v1/chain/get_table_rows", self.base_url);
        let body = serde_json::json!({
            "code": code,
            "scope": scope,
            "table": table,
            "json": true,
            "limit": limit
        });
        let resp = self
            .client
            .post(&url)
            .json(&body)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let rows: TableRowsResponse = resp
            .json()
            .map_err(|e| SignerError::NetworkError(format!("parse get_table_rows: {}", e)))?;
        Ok(rows)
    }

    /// POST `/v1/chain/get_account`
    pub fn get_account(&self, account: &str) -> Result<AccountInfo, SignerError> {
        let url = format!("{}/v1/chain/get_account", self.base_url);
        let body = serde_json::json!({ "account_name": account });
        let resp = self
            .client
            .post(&url)
            .json(&body)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let info: AccountInfo = resp
            .json()
            .map_err(|e| SignerError::NetworkError(format!("parse get_account: {}", e)))?;
        Ok(info)
    }

    /// POST `/v1/chain/get_abi`
    pub fn get_abi(&self, account: &str) -> Result<serde_json::Value, SignerError> {
        let url = format!("{}/v1/chain/get_abi", self.base_url);
        let body = serde_json::json!({ "account_name": account });
        let resp = self
            .client
            .post(&url)
            .json(&body)
            .send()
            .map_err(|e| SignerError::NetworkError(e.to_string()))?;

        let value: serde_json::Value = resp
            .json()
            .map_err(|e| SignerError::NetworkError(format!("parse get_abi: {}", e)))?;
        Ok(value)
    }

    // ── High-level helpers ──────────────────────────────────────────────

    /// Build a transaction with proper ref_block from chain info.
    pub fn build_transaction(
        &self,
        actions: Vec<Action>,
        expiration_secs: u32,
    ) -> Result<Transaction, SignerError> {
        let info = self.get_info()?;

        // Use last irreversible block for ref_block (most reliable)
        let block = self.get_block_info(info.last_irreversible_block_num)?;

        // Parse head_block_time to get expiration
        let head_time = parse_eosio_time(&info.head_block_time)?;
        let expiration = head_time + expiration_secs;

        let id_bytes = hex::decode(&block.id)
            .map_err(|e| SignerError::SerializationError(format!("invalid block id: {}", e)))?;

        let ref_block_num = (block.block_num & 0xFFFF) as u16;
        let ref_block_prefix = if id_bytes.len() >= 12 {
            u32::from_le_bytes([id_bytes[8], id_bytes[9], id_bytes[10], id_bytes[11]])
        } else {
            0
        };

        let header = TransactionHeader {
            expiration,
            ref_block_num,
            ref_block_prefix,
            max_net_usage_words: 0,
            max_cpu_usage_ms: 0,
            delay_sec: 0,
        };

        Ok(Transaction::new(header, actions))
    }

    /// Build, sign, and push a transaction in one call.
    pub fn transact(
        &self,
        actions: Vec<Action>,
        private_key: &PrivateKey,
        expiration_secs: u32,
    ) -> Result<PushTransactionResponse, SignerError> {
        let info = self.get_info()?;
        let chain_id = info.chain_id.clone();

        let block = self.get_block_info(info.last_irreversible_block_num)?;

        let head_time = parse_eosio_time(&info.head_block_time)?;
        let expiration = head_time + expiration_secs;

        let id_bytes = hex::decode(&block.id)
            .map_err(|e| SignerError::SerializationError(format!("invalid block id: {}", e)))?;

        let ref_block_num = (block.block_num & 0xFFFF) as u16;
        let ref_block_prefix = if id_bytes.len() >= 12 {
            u32::from_le_bytes([id_bytes[8], id_bytes[9], id_bytes[10], id_bytes[11]])
        } else {
            0
        };

        let header = TransactionHeader {
            expiration,
            ref_block_num,
            ref_block_prefix,
            max_net_usage_words: 0,
            max_cpu_usage_ms: 0,
            delay_sec: 0,
        };

        let tx = Transaction::new(header, actions);
        let packed = sign_transaction(&tx, &chain_id, private_key)?;
        self.push_transaction(&packed)
    }
}

/// Parse EOSIO time string `"2024-01-01T00:00:00.000"` to Unix timestamp seconds.
fn parse_eosio_time(s: &str) -> Result<u32, SignerError> {
    // Format: "YYYY-MM-DDThh:mm:ss.sss" or "YYYY-MM-DDThh:mm:ss"
    let s = s.trim();
    let s = s.split('.').next().unwrap_or(s); // strip fractional seconds

    let parts: Vec<&str> = s.split('T').collect();
    if parts.len() != 2 {
        return Err(SignerError::SerializationError(format!(
            "invalid time format: '{}'",
            s
        )));
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
        return Err(SignerError::SerializationError(format!(
            "invalid time components: '{}'",
            s
        )));
    }

    let (year, month, day) = (date_parts[0], date_parts[1], date_parts[2]);
    let (hour, min, sec) = (time_parts[0], time_parts[1], time_parts[2]);

    // Simple days-since-epoch calculation (no leap-second accuracy needed)
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

    let timestamp = days * 86400 + hour * 3600 + min * 60 + sec;
    Ok(timestamp)
}

fn is_leap_year(y: u32) -> bool {
    (y % 4 == 0 && y % 100 != 0) || (y % 400 == 0)
}
