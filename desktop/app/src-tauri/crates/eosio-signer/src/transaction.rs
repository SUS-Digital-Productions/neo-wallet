use serde::{Deserialize, Serialize};

// ── Name encoding (standalone, no dependency on eosio-core) ─────────────

fn char_to_value(c: u8) -> u8 {
    match c {
        b'.' => 0,
        b'1'..=b'5' => (c - b'1') + 1,
        b'a'..=b'z' => (c - b'a') + 6,
        _ => 0,
    }
}

pub fn name_to_u64(s: &str) -> u64 {
    let bytes = s.as_bytes();
    let mut value: u64 = 0;
    let len = bytes.len().min(12);
    for i in 0..len {
        let c = char_to_value(bytes[i]);
        value |= (c as u64 & 0x1f) << (64 - 5 * (i + 1));
    }
    if bytes.len() > 12 {
        let c = char_to_value(bytes[12]);
        value |= (c as u64) & 0x0f;
    }
    value
}

// ── Varuint32 encoding ──────────────────────────────────────────────────

pub fn write_varuint32(buf: &mut Vec<u8>, mut val: u32) {
    loop {
        let mut byte = (val & 0x7f) as u8;
        val >>= 7;
        if val > 0 {
            byte |= 0x80;
        }
        buf.push(byte);
        if val == 0 {
            break;
        }
    }
}

// ── Core transaction types ──────────────────────────────────────────────

/// An EOSIO permission level (actor@permission).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PermissionLevel {
    pub actor: String,
    pub permission: String,
}

impl PermissionLevel {
    pub fn new(actor: &str, permission: &str) -> Self {
        Self {
            actor: actor.to_string(),
            permission: permission.to_string(),
        }
    }

    pub fn active(actor: &str) -> Self {
        Self::new(actor, "active")
    }

    pub fn serialize(&self, buf: &mut Vec<u8>) {
        buf.extend_from_slice(&name_to_u64(&self.actor).to_le_bytes());
        buf.extend_from_slice(&name_to_u64(&self.permission).to_le_bytes());
    }
}

/// An EOSIO action.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Action {
    pub account: String,
    pub name: String,
    pub authorization: Vec<PermissionLevel>,
    /// Hex-encoded action data, or raw bytes set via `set_data_bytes`.
    #[serde(default)]
    pub data: String,
    /// Raw binary action data (takes precedence over `data` hex string).
    #[serde(skip)]
    pub data_bytes: Option<Vec<u8>>,
}

impl Action {
    pub fn new(account: &str, name: &str, authorization: Vec<PermissionLevel>) -> Self {
        Self {
            account: account.to_string(),
            name: name.to_string(),
            authorization,
            data: String::new(),
            data_bytes: None,
        }
    }

    /// Set action data from raw bytes.
    pub fn set_data_bytes(&mut self, data: Vec<u8>) {
        self.data_bytes = Some(data);
    }

    /// Set action data from a hex string.
    pub fn set_data_hex(&mut self, hex: &str) {
        self.data = hex.to_string();
    }

    /// Get the raw binary action data.
    pub fn get_data_bytes(&self) -> Result<Vec<u8>, crate::SignerError> {
        if let Some(ref bytes) = self.data_bytes {
            Ok(bytes.clone())
        } else if !self.data.is_empty() {
            hex::decode(&self.data)
                .map_err(|e| crate::SignerError::SerializationError(format!("invalid action data hex: {}", e)))
        } else {
            Ok(Vec::new())
        }
    }

    pub fn serialize(&self) -> Result<Vec<u8>, crate::SignerError> {
        let mut buf = Vec::new();

        // account (name)
        buf.extend_from_slice(&name_to_u64(&self.account).to_le_bytes());
        // action name
        buf.extend_from_slice(&name_to_u64(&self.name).to_le_bytes());

        // authorization vector
        write_varuint32(&mut buf, self.authorization.len() as u32);
        for auth in &self.authorization {
            auth.serialize(&mut buf);
        }

        // data vector
        let data = self.get_data_bytes()?;
        write_varuint32(&mut buf, data.len() as u32);
        buf.extend_from_slice(&data);

        Ok(buf)
    }
}

/// Transaction extension (type_id + data).
#[derive(Debug, Clone, Default)]
pub struct TransactionExtension {
    pub type_id: u16,
    pub data: Vec<u8>,
}

impl TransactionExtension {
    pub fn serialize(&self, buf: &mut Vec<u8>) {
        buf.extend_from_slice(&self.type_id.to_le_bytes());
        write_varuint32(buf, self.data.len() as u32);
        buf.extend_from_slice(&self.data);
    }
}

/// EOSIO transaction header (common fields for all transaction types).
#[derive(Debug, Clone)]
pub struct TransactionHeader {
    /// Expiration time (seconds since Unix epoch).
    pub expiration: u32,
    /// Reference block number (lower 16 bits).
    pub ref_block_num: u16,
    /// Reference block prefix (first 4 bytes of block ID, after the first 4).
    pub ref_block_prefix: u32,
    /// Maximum NET bandwidth usage (in bytes). 0 = no limit.
    pub max_net_usage_words: u32,
    /// Maximum CPU bandwidth usage (in microseconds). 0 = no limit.
    pub max_cpu_usage_ms: u8,
    /// Delay in seconds before the transaction executes.
    pub delay_sec: u32,
}

impl Default for TransactionHeader {
    fn default() -> Self {
        Self {
            expiration: 0,
            ref_block_num: 0,
            ref_block_prefix: 0,
            max_net_usage_words: 0,
            max_cpu_usage_ms: 0,
            delay_sec: 0,
        }
    }
}

impl TransactionHeader {
    pub fn serialize(&self, buf: &mut Vec<u8>) {
        buf.extend_from_slice(&self.expiration.to_le_bytes());
        buf.extend_from_slice(&self.ref_block_num.to_le_bytes());
        buf.extend_from_slice(&self.ref_block_prefix.to_le_bytes());
        write_varuint32(buf, self.max_net_usage_words);
        buf.push(self.max_cpu_usage_ms);
        write_varuint32(buf, self.delay_sec);
    }
}

/// A full EOSIO transaction.
#[derive(Debug, Clone)]
pub struct Transaction {
    pub header: TransactionHeader,
    pub context_free_actions: Vec<Action>,
    pub actions: Vec<Action>,
    pub extensions: Vec<TransactionExtension>,
}

impl Transaction {
    pub fn new(header: TransactionHeader, actions: Vec<Action>) -> Self {
        Self {
            header,
            context_free_actions: Vec::new(),
            actions,
            extensions: Vec::new(),
        }
    }

    /// Serialize the transaction to EOSIO binary format.
    pub fn serialize(&self) -> Result<Vec<u8>, crate::SignerError> {
        let mut buf = Vec::new();

        // Header
        self.header.serialize(&mut buf);

        // Context-free actions
        write_varuint32(&mut buf, self.context_free_actions.len() as u32);
        for action in &self.context_free_actions {
            buf.extend_from_slice(&action.serialize()?);
        }

        // Actions
        write_varuint32(&mut buf, self.actions.len() as u32);
        for action in &self.actions {
            buf.extend_from_slice(&action.serialize()?);
        }

        // Extensions
        write_varuint32(&mut buf, self.extensions.len() as u32);
        for ext in &self.extensions {
            ext.serialize(&mut buf);
        }

        Ok(buf)
    }

    /// Set the transaction header from chain info and a reference block.
    pub fn set_ref_block(&mut self, block_id: &str, expiration_seconds: u32) {
        let id_bytes = hex::decode(block_id).unwrap_or_default();
        if id_bytes.len() >= 8 {
            // ref_block_num = block_num & 0xFFFF (bytes 0..4 big-endian, lower 16 bits)
            let block_num = u32::from_be_bytes([id_bytes[0], id_bytes[1], id_bytes[2], id_bytes[3]]);
            self.header.ref_block_num = (block_num & 0xFFFF) as u16;

            // ref_block_prefix = bytes 8..12 of block ID as little-endian u32
            if id_bytes.len() >= 12 {
                self.header.ref_block_prefix = u32::from_le_bytes([
                    id_bytes[8],
                    id_bytes[9],
                    id_bytes[10],
                    id_bytes[11],
                ]);
            }
        }

        // Set expiration
        let head_time = u32::from_be_bytes([id_bytes[0], id_bytes[1], id_bytes[2], id_bytes[3]]);
        // Expiration is typically head_block_time + N seconds
        // The caller provides the absolute expiration
        self.header.expiration = expiration_seconds;
        let _ = head_time; // suppress unused
    }
}

/// A signed transaction ready for broadcast.
#[derive(Debug, Clone, Serialize)]
pub struct SignedTransaction {
    pub compression: String,
    pub transaction: serde_json::Value,
    pub signatures: Vec<String>,
}

/// Packed transaction for the `/v1/chain/push_transaction` endpoint.
#[derive(Debug, Clone, Serialize)]
pub struct PackedTransaction {
    pub signatures: Vec<String>,
    pub compression: u8,
    pub packed_context_free_data: String,
    pub packed_trx: String,
}

impl PackedTransaction {
    pub fn new(signatures: Vec<String>, packed_trx: Vec<u8>) -> Self {
        Self {
            signatures,
            compression: 0,
            packed_context_free_data: String::new(),
            packed_trx: hex::encode(packed_trx),
        }
    }
}
