use crate::abi::AbiDef;
use crate::resolver::{resolve_type, ResolvedType};
use crate::AbiError;
use serde_json::Value;

/// Encode a JSON value into EOSIO binary according to the resolved type.
pub fn encode(
    abi: &AbiDef,
    resolved: &ResolvedType,
    value: &Value,
    buf: &mut Vec<u8>,
) -> Result<(), AbiError> {
    match resolved {
        ResolvedType::Bool => {
            let b = match value {
                Value::Bool(b) => *b,
                Value::Number(n) => n.as_u64().map(|v| v != 0).unwrap_or(false),
                _ => return Err(AbiError::type_mismatch("bool", value)),
            };
            buf.push(u8::from(b));
        }

        ResolvedType::Int8 => {
            let v = as_i64(value, "int8")? as i8;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::Int16 => {
            let v = as_i64(value, "int16")? as i16;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::Int32 => {
            let v = as_i64(value, "int32")? as i32;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::Int64 => {
            let v = as_i64(value, "int64")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::Int128 => {
            let v = as_i128(value, "int128")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }

        ResolvedType::UInt8 => {
            let v = as_u64(value, "uint8")? as u8;
            buf.push(v);
        }
        ResolvedType::UInt16 => {
            let v = as_u64(value, "uint16")? as u16;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::UInt32 => {
            let v = as_u64(value, "uint32")? as u32;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::UInt64 => {
            let v = as_u64(value, "uint64")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::UInt128 => {
            let v = as_u128(value, "uint128")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }

        ResolvedType::Float32 => {
            let v = as_f64(value, "float32")? as f32;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::Float64 => {
            let v = as_f64(value, "float64")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }

        ResolvedType::VarUint32 => {
            let v = as_u64(value, "varuint32")? as u32;
            write_varuint32(buf, v);
        }
        ResolvedType::VarInt32 => {
            let v = as_i64(value, "varint32")? as i32;
            let zigzag = ((v << 1) ^ (v >> 31)) as u32;
            write_varuint32(buf, zigzag);
        }

        ResolvedType::Name => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("name (string)", value))?;
            let encoded = crate::name_to_u64(s);
            buf.extend_from_slice(&encoded.to_le_bytes());
        }

        ResolvedType::String => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("string", value))?;
            write_varuint32(buf, s.len() as u32);
            buf.extend_from_slice(s.as_bytes());
        }

        ResolvedType::Bytes => {
            let hex = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("bytes (hex string)", value))?;
            let bytes = crate::hex_to_bytes(hex)?;
            write_varuint32(buf, bytes.len() as u32);
            buf.extend_from_slice(&bytes);
        }

        ResolvedType::Checksum256 => {
            encode_checksum(value, 32, "checksum256", buf)?;
        }
        ResolvedType::Checksum160 => {
            encode_checksum(value, 20, "checksum160", buf)?;
        }
        ResolvedType::Checksum512 => {
            encode_checksum(value, 64, "checksum512", buf)?;
        }

        ResolvedType::TimePoint => {
            let v = as_u64(value, "time_point")?;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::TimePointSec => {
            let v = as_u64(value, "time_point_sec")? as u32;
            buf.extend_from_slice(&v.to_le_bytes());
        }
        ResolvedType::BlockTimestampType => {
            let v = as_u64(value, "block_timestamp_type")? as u32;
            buf.extend_from_slice(&v.to_le_bytes());
        }

        ResolvedType::Asset => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("asset (string)", value))?;
            let (amount, symbol) = crate::parse_asset(s)?;
            buf.extend_from_slice(&amount.to_le_bytes());
            buf.extend_from_slice(&symbol.to_le_bytes());
        }

        ResolvedType::ExtendedAsset => {
            let obj = value
                .as_object()
                .ok_or_else(|| AbiError::type_mismatch("extended_asset (object)", value))?;

            let qty_str = obj
                .get("quantity")
                .and_then(|v| v.as_str())
                .ok_or_else(|| AbiError::MissingField("quantity".into()))?;
            let (amount, symbol) = crate::parse_asset(qty_str)?;
            buf.extend_from_slice(&amount.to_le_bytes());
            buf.extend_from_slice(&symbol.to_le_bytes());

            let contract_str = obj
                .get("contract")
                .and_then(|v| v.as_str())
                .ok_or_else(|| AbiError::MissingField("contract".into()))?;
            buf.extend_from_slice(&crate::name_to_u64(contract_str).to_le_bytes());
        }

        ResolvedType::Symbol => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("symbol (string)", value))?;
            let sym = crate::parse_symbol(s)?;
            buf.extend_from_slice(&sym.to_le_bytes());
        }

        ResolvedType::SymbolCode => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("symbol_code (string)", value))?;
            let code = crate::symbol_code_to_u64(s);
            buf.extend_from_slice(&code.to_le_bytes());
        }

        ResolvedType::PublicKey => {
            // Type byte (0 = K1) + 33-byte compressed key
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("public_key (string)", value))?;
            let bytes = crate::hex_to_bytes(s)?;
            buf.extend_from_slice(&bytes);
        }

        ResolvedType::Signature => {
            let s = value
                .as_str()
                .ok_or_else(|| AbiError::type_mismatch("signature (string)", value))?;
            let bytes = crate::hex_to_bytes(s)?;
            buf.extend_from_slice(&bytes);
        }

        ResolvedType::Struct(name) => {
            encode_struct(abi, name, value, buf)?;
        }

        ResolvedType::Array(inner) => {
            let arr = value
                .as_array()
                .ok_or_else(|| AbiError::type_mismatch("array", value))?;
            write_varuint32(buf, arr.len() as u32);
            for elem in arr {
                encode(abi, inner, elem, buf)?;
            }
        }

        ResolvedType::Optional(inner) => {
            if value.is_null() {
                buf.push(0);
            } else {
                buf.push(1);
                encode(abi, inner, value, buf)?;
            }
        }

        ResolvedType::Variant(name) => {
            encode_variant(abi, name, value, buf)?;
        }
    }
    Ok(())
}

fn encode_struct(
    abi: &AbiDef,
    struct_name: &str,
    value: &Value,
    buf: &mut Vec<u8>,
) -> Result<(), AbiError> {
    let st = abi
        .structs
        .iter()
        .find(|s| s.name == struct_name)
        .ok_or_else(|| AbiError::UnknownType(struct_name.to_string()))?;

    let obj = value.as_object().ok_or_else(|| {
        AbiError::type_mismatch(&format!("struct '{}' (object)", struct_name), value)
    })?;

    // Encode inherited base fields first
    if !st.base.is_empty() {
        encode_struct(abi, &st.base, value, buf)?;
    }

    for field in &st.fields {
        let field_value = obj.get(&field.name).ok_or_else(|| {
            AbiError::MissingField(format!("{}.{}", struct_name, field.name))
        })?;
        let resolved = resolve_type(abi, &field.type_)
            .ok_or_else(|| AbiError::UnknownType(field.type_.clone()))?;
        encode(abi, &resolved, field_value, buf)?;
    }

    Ok(())
}

fn encode_variant(
    abi: &AbiDef,
    variant_name: &str,
    value: &Value,
    buf: &mut Vec<u8>,
) -> Result<(), AbiError> {
    let vdef = abi
        .variants
        .iter()
        .find(|v| v.name == variant_name)
        .ok_or_else(|| AbiError::UnknownType(variant_name.to_string()))?;

    // Variant JSON format: ["type_name", value]
    let arr = value
        .as_array()
        .ok_or_else(|| AbiError::type_mismatch("variant ([type, value])", value))?;
    if arr.len() != 2 {
        return Err(AbiError::InvalidData(
            "variant must be [type_name, value]".into(),
        ));
    }

    let type_name = arr[0]
        .as_str()
        .ok_or_else(|| AbiError::type_mismatch("variant type name (string)", &arr[0]))?;

    let idx = vdef
        .types
        .iter()
        .position(|t| t == type_name)
        .ok_or_else(|| {
            AbiError::InvalidData(format!(
                "unknown variant type '{}' in '{}'",
                type_name, variant_name
            ))
        })?;

    write_varuint32(buf, idx as u32);

    let resolved = resolve_type(abi, type_name)
        .ok_or_else(|| AbiError::UnknownType(type_name.to_string()))?;
    encode(abi, &resolved, &arr[1], buf)?;

    Ok(())
}

fn encode_checksum(
    value: &Value,
    expected_len: usize,
    type_name: &str,
    buf: &mut Vec<u8>,
) -> Result<(), AbiError> {
    let hex = value
        .as_str()
        .ok_or_else(|| AbiError::type_mismatch(&format!("{} (hex string)", type_name), value))?;
    let bytes = crate::hex_to_bytes(hex)?;
    if bytes.len() != expected_len {
        return Err(AbiError::InvalidData(format!(
            "{} must be {} bytes, got {}",
            type_name,
            expected_len,
            bytes.len()
        )));
    }
    buf.extend_from_slice(&bytes);
    Ok(())
}

// ── JSON value helpers ──────────────────────────────────────────────────

fn as_i64(value: &Value, expected: &str) -> Result<i64, AbiError> {
    match value {
        Value::Number(n) => n
            .as_i64()
            .ok_or_else(|| AbiError::type_mismatch(expected, value)),
        Value::String(s) => s
            .parse::<i64>()
            .map_err(|_| AbiError::type_mismatch(expected, value)),
        _ => Err(AbiError::type_mismatch(expected, value)),
    }
}

fn as_u64(value: &Value, expected: &str) -> Result<u64, AbiError> {
    match value {
        Value::Number(n) => n
            .as_u64()
            .ok_or_else(|| AbiError::type_mismatch(expected, value)),
        Value::String(s) => s
            .parse::<u64>()
            .map_err(|_| AbiError::type_mismatch(expected, value)),
        _ => Err(AbiError::type_mismatch(expected, value)),
    }
}

fn as_i128(value: &Value, expected: &str) -> Result<i128, AbiError> {
    match value {
        Value::Number(n) => n
            .as_i64()
            .map(|v| v as i128)
            .ok_or_else(|| AbiError::type_mismatch(expected, value)),
        Value::String(s) => s
            .parse::<i128>()
            .map_err(|_| AbiError::type_mismatch(expected, value)),
        _ => Err(AbiError::type_mismatch(expected, value)),
    }
}

fn as_u128(value: &Value, expected: &str) -> Result<u128, AbiError> {
    match value {
        Value::Number(n) => n
            .as_u64()
            .map(|v| v as u128)
            .ok_or_else(|| AbiError::type_mismatch(expected, value)),
        Value::String(s) => s
            .parse::<u128>()
            .map_err(|_| AbiError::type_mismatch(expected, value)),
        _ => Err(AbiError::type_mismatch(expected, value)),
    }
}

fn as_f64(value: &Value, expected: &str) -> Result<f64, AbiError> {
    match value {
        Value::Number(n) => n
            .as_f64()
            .ok_or_else(|| AbiError::type_mismatch(expected, value)),
        Value::String(s) => s
            .parse::<f64>()
            .map_err(|_| AbiError::type_mismatch(expected, value)),
        _ => Err(AbiError::type_mismatch(expected, value)),
    }
}

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
