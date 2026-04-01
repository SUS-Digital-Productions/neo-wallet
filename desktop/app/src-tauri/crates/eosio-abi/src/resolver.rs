use crate::abi::AbiDef;

/// A fully-resolved ABI type used by the encoder and decoder.
#[derive(Debug, Clone)]
pub enum ResolvedType {
    Bool,
    Int8,
    Int16,
    Int32,
    Int64,
    Int128,
    UInt8,
    UInt16,
    UInt32,
    UInt64,
    UInt128,
    Float32,
    Float64,
    VarUint32,
    VarInt32,
    Name,
    String,
    Bytes,
    Checksum256,
    Checksum160,
    Checksum512,
    TimePoint,
    TimePointSec,
    BlockTimestampType,
    Asset,
    ExtendedAsset,
    Symbol,
    SymbolCode,
    PublicKey,
    Signature,
    Struct(std::string::String),
    Array(Box<ResolvedType>),
    Optional(Box<ResolvedType>),
    Variant(std::string::String),
}

/// Resolve a type name string (e.g. `"uint64"`, `"name[]"`, `"my_struct?"`)
/// against an ABI definition into a concrete `ResolvedType`.
pub fn resolve_type(abi: &AbiDef, type_name: &str) -> Option<ResolvedType> {
    resolve_inner(abi, type_name, 0)
}

fn resolve_inner(abi: &AbiDef, type_name: &str, depth: usize) -> Option<ResolvedType> {
    if depth > 32 {
        return None;
    }

    let name = type_name.trim();

    // Array suffix
    if let Some(inner) = name.strip_suffix("[]") {
        let resolved = resolve_inner(abi, inner, depth + 1)?;
        return Some(ResolvedType::Array(Box::new(resolved)));
    }

    // Optional suffix
    if let Some(inner) = name.strip_suffix('?') {
        let resolved = resolve_inner(abi, inner, depth + 1)?;
        return Some(ResolvedType::Optional(Box::new(resolved)));
    }

    // Type aliases
    for td in &abi.types {
        if td.new_type_name == name {
            return resolve_inner(abi, &td.type_, depth + 1);
        }
    }

    // Built-in types
    let builtin = match name {
        "bool" => Some(ResolvedType::Bool),
        "int8" => Some(ResolvedType::Int8),
        "int16" => Some(ResolvedType::Int16),
        "int32" => Some(ResolvedType::Int32),
        "int64" => Some(ResolvedType::Int64),
        "int128" => Some(ResolvedType::Int128),
        "uint8" => Some(ResolvedType::UInt8),
        "uint16" => Some(ResolvedType::UInt16),
        "uint32" => Some(ResolvedType::UInt32),
        "uint64" => Some(ResolvedType::UInt64),
        "uint128" => Some(ResolvedType::UInt128),
        "float32" => Some(ResolvedType::Float32),
        "float64" => Some(ResolvedType::Float64),
        "varuint32" => Some(ResolvedType::VarUint32),
        "varint32" => Some(ResolvedType::VarInt32),
        "name" | "account_name" | "action_name" | "table_name" | "permission_name"
        | "scope_name" => Some(ResolvedType::Name),
        "string" => Some(ResolvedType::String),
        "bytes" => Some(ResolvedType::Bytes),
        "checksum256" | "transaction_id_type" | "block_id_type" => {
            Some(ResolvedType::Checksum256)
        }
        "checksum160" => Some(ResolvedType::Checksum160),
        "checksum512" => Some(ResolvedType::Checksum512),
        "time_point" => Some(ResolvedType::TimePoint),
        "time_point_sec" => Some(ResolvedType::TimePointSec),
        "block_timestamp_type" => Some(ResolvedType::BlockTimestampType),
        "asset" => Some(ResolvedType::Asset),
        "extended_asset" => Some(ResolvedType::ExtendedAsset),
        "symbol" => Some(ResolvedType::Symbol),
        "symbol_code" => Some(ResolvedType::SymbolCode),
        "public_key" => Some(ResolvedType::PublicKey),
        "signature" => Some(ResolvedType::Signature),
        _ => None,
    };
    if builtin.is_some() {
        return builtin;
    }

    // Struct names
    if abi.structs.iter().any(|s| s.name == name) {
        return Some(ResolvedType::Struct(name.to_string()));
    }

    // Variant names
    if abi.variants.iter().any(|v| v.name == name) {
        return Some(ResolvedType::Variant(name.to_string()));
    }

    None
}
