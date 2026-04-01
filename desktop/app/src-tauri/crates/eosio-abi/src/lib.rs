//! # eosio-abi
//!
//! Dynamic ABI serialization and deserialization for EOSIO/WAX.
//!
//! This crate can encode JSON action data into EOSIO binary format and decode
//! binary data back into JSON, all driven by a standard ABI definition at runtime.
//!
//! # Example
//!
//! ```rust
//! use eosio_abi::AbiCodec;
//!
//! let abi_json = r#"{
//!   "version": "eosio::abi/1.2",
//!   "structs": [{
//!     "name": "transfer",
//!     "base": "",
//!     "fields": [
//!       {"name": "from", "type": "name"},
//!       {"name": "to", "type": "name"},
//!       {"name": "quantity", "type": "asset"},
//!       {"name": "memo", "type": "string"}
//!     ]
//!   }],
//!   "actions": [{"name": "transfer", "type": "transfer", "ricardian_contract": ""}],
//!   "tables": [],
//!   "types": [],
//!   "variants": []
//! }"#;
//!
//! let codec = AbiCodec::from_json(abi_json).unwrap();
//!
//! let action_data = serde_json::json!({
//!     "from": "alice",
//!     "to": "bob",
//!     "quantity": "1.0000 WAX",
//!     "memo": "hello"
//! });
//!
//! let bytes = codec.encode_action("transfer", &action_data).unwrap();
//! let decoded = codec.decode_action("transfer", &bytes).unwrap();
//! assert_eq!(decoded["memo"], "hello");
//! ```

pub mod abi;
mod decoder;
mod encoder;
mod resolver;

pub use abi::*;
pub use decoder::BinaryReader;
pub use resolver::ResolvedType;

use std::fmt;

// ── Error type ──────────────────────────────────────────────────────────

/// Errors produced by ABI encoding/decoding.
#[derive(Debug)]
pub enum AbiError {
    /// JSON parse error.
    JsonError(String),
    /// Unknown or unresolvable type name.
    UnknownType(String),
    /// Required struct field is missing.
    MissingField(String),
    /// Value does not match expected type.
    TypeMismatch(String),
    /// Invalid data format.
    InvalidData(String),
    /// Invalid hex string.
    InvalidHex(String),
    /// Read past end of binary data.
    ReadOverrun,
    /// Action not found in ABI.
    UnknownAction(String),
    /// Table not found in ABI.
    UnknownTable(String),
}

impl AbiError {
    pub fn type_mismatch(expected: &str, got: &serde_json::Value) -> Self {
        let got_desc = match got {
            serde_json::Value::Null => "null",
            serde_json::Value::Bool(_) => "bool",
            serde_json::Value::Number(_) => "number",
            serde_json::Value::String(_) => "string",
            serde_json::Value::Array(_) => "array",
            serde_json::Value::Object(_) => "object",
        };
        AbiError::TypeMismatch(format!("expected {}, got {}", expected, got_desc))
    }
}

impl fmt::Display for AbiError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AbiError::JsonError(msg) => write!(f, "JSON error: {}", msg),
            AbiError::UnknownType(t) => write!(f, "unknown type: {}", t),
            AbiError::MissingField(s) => write!(f, "missing field: {}", s),
            AbiError::TypeMismatch(s) => write!(f, "type mismatch: {}", s),
            AbiError::InvalidData(s) => write!(f, "invalid data: {}", s),
            AbiError::InvalidHex(s) => write!(f, "invalid hex: {}", s),
            AbiError::ReadOverrun => write!(f, "read past end of data"),
            AbiError::UnknownAction(a) => write!(f, "unknown action: {}", a),
            AbiError::UnknownTable(t) => write!(f, "unknown table: {}", t),
        }
    }
}

impl std::error::Error for AbiError {}

// ── Codec ───────────────────────────────────────────────────────────────

/// Dynamic EOSIO ABI encoder/decoder.
///
/// Holds a parsed ABI definition and provides methods to encode JSON → binary
/// and decode binary → JSON for actions, tables, and arbitrary types.
pub struct AbiCodec {
    abi: AbiDef,
}

impl AbiCodec {
    /// Parse an ABI from its JSON representation.
    pub fn from_json(json: &str) -> Result<Self, AbiError> {
        let abi: AbiDef =
            serde_json::from_str(json).map_err(|e| AbiError::JsonError(e.to_string()))?;
        Ok(Self { abi })
    }

    /// Create a codec from an already-parsed ABI definition.
    pub fn new(abi: AbiDef) -> Self {
        Self { abi }
    }

    /// Get a reference to the underlying ABI definition.
    pub fn abi(&self) -> &AbiDef {
        &self.abi
    }

    /// Encode action data from JSON into EOSIO binary.
    ///
    /// Looks up the action by name, resolves its struct type, and encodes
    /// each field according to the ABI.
    pub fn encode_action(
        &self,
        action: &str,
        data: &serde_json::Value,
    ) -> Result<Vec<u8>, AbiError> {
        let act = self
            .abi
            .actions
            .iter()
            .find(|a| a.name == action)
            .ok_or_else(|| AbiError::UnknownAction(action.to_string()))?;
        self.encode_type(&act.type_, data)
    }

    /// Decode action binary data into JSON.
    pub fn decode_action(
        &self,
        action: &str,
        data: &[u8],
    ) -> Result<serde_json::Value, AbiError> {
        let act = self
            .abi
            .actions
            .iter()
            .find(|a| a.name == action)
            .ok_or_else(|| AbiError::UnknownAction(action.to_string()))?;
        self.decode_type(&act.type_, data)
    }

    /// Encode a table row from JSON into EOSIO binary.
    pub fn encode_table_row(
        &self,
        table: &str,
        data: &serde_json::Value,
    ) -> Result<Vec<u8>, AbiError> {
        let tbl = self
            .abi
            .tables
            .iter()
            .find(|t| t.name == table)
            .ok_or_else(|| AbiError::UnknownTable(table.to_string()))?;
        self.encode_type(&tbl.type_, data)
    }

    /// Decode a table row from EOSIO binary into JSON.
    pub fn decode_table_row(
        &self,
        table: &str,
        data: &[u8],
    ) -> Result<serde_json::Value, AbiError> {
        let tbl = self
            .abi
            .tables
            .iter()
            .find(|t| t.name == table)
            .ok_or_else(|| AbiError::UnknownTable(table.to_string()))?;
        self.decode_type(&tbl.type_, data)
    }

    /// Encode a value of an arbitrary named type into EOSIO binary.
    pub fn encode_type(
        &self,
        type_name: &str,
        data: &serde_json::Value,
    ) -> Result<Vec<u8>, AbiError> {
        let resolved = resolver::resolve_type(&self.abi, type_name)
            .ok_or_else(|| AbiError::UnknownType(type_name.to_string()))?;
        let mut buf = Vec::new();
        encoder::encode(&self.abi, &resolved, data, &mut buf)?;
        Ok(buf)
    }

    /// Decode EOSIO binary into JSON for an arbitrary named type.
    pub fn decode_type(
        &self,
        type_name: &str,
        data: &[u8],
    ) -> Result<serde_json::Value, AbiError> {
        let resolved = resolver::resolve_type(&self.abi, type_name)
            .ok_or_else(|| AbiError::UnknownType(type_name.to_string()))?;
        let mut reader = decoder::BinaryReader::new(data);
        decoder::decode(&self.abi, &resolved, &mut reader)
    }

    /// Resolve a type name string against the ABI.
    pub fn resolve(&self, type_name: &str) -> Option<ResolvedType> {
        resolver::resolve_type(&self.abi, type_name)
    }
}

// ── EOSIO name encoding ─────────────────────────────────────────────────

fn char_to_value(c: u8) -> u8 {
    match c {
        b'.' => 0,
        b'1'..=b'5' => (c - b'1') + 1,
        b'a'..=b'z' => (c - b'a') + 6,
        _ => 0,
    }
}

fn value_to_char(v: u8) -> u8 {
    match v {
        0 => b'.',
        1..=5 => b'1' + (v - 1),
        6..=31 => b'a' + (v - 6),
        _ => b'.',
    }
}

/// Encode an EOSIO name string into its u64 representation.
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

/// Decode a u64 into an EOSIO name string.
pub fn u64_to_name(value: u64) -> String {
    let mut buf = [0u8; 13];
    let mut tmp = value;
    let mut len = 0usize;
    for i in 0..13usize {
        let shift = if i < 12 { 64 - 5 * (i + 1) } else { 0 };
        let bits = if i < 12 { 5 } else { 4 };
        let mask = (1u64 << bits) - 1;
        let c = (tmp >> shift) & mask;
        tmp &= !(mask << shift);
        let ch = value_to_char(c as u8);
        if ch != b'.' {
            len = i + 1;
        }
        buf[i] = ch;
    }
    // SAFETY: we only write ASCII bytes
    std::str::from_utf8(&buf[..len]).unwrap().to_string()
}

// ── Asset / Symbol helpers ──────────────────────────────────────────────

/// Parse an asset string like `"1.0000 WAX"` into `(amount_i64, symbol_u64)`.
pub fn parse_asset(s: &str) -> Result<(i64, u64), AbiError> {
    let s = s.trim();
    let space_idx = s
        .find(' ')
        .ok_or_else(|| AbiError::InvalidData(format!("invalid asset: '{}'", s)))?;

    let amount_str = &s[..space_idx];
    let sym_str = &s[space_idx + 1..];

    // Determine precision from decimal digits
    let precision = if let Some(dot_idx) = amount_str.find('.') {
        (amount_str.len() - dot_idx - 1) as u8
    } else {
        0
    };

    // Parse amount: remove '.', parse as integer
    let clean: String = amount_str.replace('.', "");
    let amount: i64 = clean
        .parse()
        .map_err(|_| AbiError::InvalidData(format!("invalid asset amount: '{}'", amount_str)))?;

    let symbol = make_symbol(precision, sym_str);
    Ok((amount, symbol))
}

/// Format an asset from raw `(amount_i64, symbol_u64)` back to string `"1.0000 WAX"`.
pub fn format_asset(amount: i64, symbol: u64) -> String {
    let precision = (symbol & 0xff) as u8;
    let code = u64_to_symbol_code(symbol >> 8);

    if precision == 0 {
        return format!("{} {}", amount, code);
    }

    let divisor = 10i64.pow(precision as u32);
    let is_negative = amount < 0;
    let abs = amount.unsigned_abs();

    let whole = abs / divisor as u64;
    let frac = abs % divisor as u64;

    let sign = if is_negative { "-" } else { "" };
    format!(
        "{}{}.{:0>width$} {}",
        sign,
        whole,
        frac,
        code,
        width = precision as usize
    )
}

/// Parse a symbol string like `"4,WAX"` into its u64 representation.
pub fn parse_symbol(s: &str) -> Result<u64, AbiError> {
    let s = s.trim();
    let comma_idx = s
        .find(',')
        .ok_or_else(|| AbiError::InvalidData(format!("invalid symbol: '{}' (expected precision,CODE)", s)))?;

    let precision: u8 = s[..comma_idx]
        .parse()
        .map_err(|_| AbiError::InvalidData(format!("invalid symbol precision: '{}'", &s[..comma_idx])))?;

    let code_str = &s[comma_idx + 1..];
    Ok(make_symbol(precision, code_str))
}

/// Format a raw symbol u64 back to `"4,WAX"`.
pub fn format_symbol(raw: u64) -> String {
    let precision = (raw & 0xff) as u8;
    let code = u64_to_symbol_code(raw >> 8);
    format!("{},{}", precision, code)
}

fn make_symbol(precision: u8, code: &str) -> u64 {
    let mut raw = precision as u64;
    let bytes = code.as_bytes();
    let len = bytes.len().min(7);
    for i in 0..len {
        raw |= (bytes[i] as u64) << (8 * (i + 1));
    }
    raw
}

/// Encode a symbol code string (up to 7 chars) into a u64.
pub fn symbol_code_to_u64(s: &str) -> u64 {
    let bytes = s.as_bytes();
    let mut raw: u64 = 0;
    let len = bytes.len().min(7);
    for i in 0..len {
        raw |= (bytes[i] as u64) << (8 * i);
    }
    raw
}

/// Decode a raw symbol code u64 (no precision byte) into a string.
pub fn u64_to_symbol_code(raw: u64) -> String {
    let mut buf = Vec::with_capacity(7);
    for i in 0..7 {
        let c = ((raw >> (8 * i)) & 0xff) as u8;
        if c == 0 {
            break;
        }
        buf.push(c);
    }
    String::from_utf8(buf).unwrap_or_default()
}

// ── Hex helpers ─────────────────────────────────────────────────────────

/// Decode a hex string into bytes.
pub fn hex_to_bytes(hex: &str) -> Result<Vec<u8>, AbiError> {
    let hex = hex.strip_prefix("0x").or_else(|| hex.strip_prefix("0X")).unwrap_or(hex);

    if hex.len() % 2 != 0 {
        return Err(AbiError::InvalidHex("odd length".into()));
    }

    let mut bytes = Vec::with_capacity(hex.len() / 2);
    for i in (0..hex.len()).step_by(2) {
        let byte = u8::from_str_radix(&hex[i..i + 2], 16)
            .map_err(|_| AbiError::InvalidHex(format!("invalid hex at position {}", i)))?;
        bytes.push(byte);
    }
    Ok(bytes)
}

/// Encode bytes as a lowercase hex string.
pub fn bytes_to_hex(bytes: &[u8]) -> String {
    let mut s = String::with_capacity(bytes.len() * 2);
    for b in bytes {
        s.push_str(&format!("{:02x}", b));
    }
    s
}
