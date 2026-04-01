use crate::abi::AbiDef;
use crate::resolver::{resolve_type, ResolvedType};
use crate::AbiError;
use serde_json::Value;

/// A zero-copy binary reader over a byte slice.
pub struct BinaryReader<'a> {
    data: &'a [u8],
    pos: usize,
}

impl<'a> BinaryReader<'a> {
    pub fn new(data: &'a [u8]) -> Self {
        Self { data, pos: 0 }
    }

    pub fn remaining(&self) -> usize {
        self.data.len().saturating_sub(self.pos)
    }

    pub fn position(&self) -> usize {
        self.pos
    }

    fn read_byte(&mut self) -> Result<u8, AbiError> {
        if self.pos >= self.data.len() {
            return Err(AbiError::ReadOverrun);
        }
        let b = self.data[self.pos];
        self.pos += 1;
        Ok(b)
    }

    fn read_bytes(&mut self, n: usize) -> Result<&'a [u8], AbiError> {
        if self.pos + n > self.data.len() {
            return Err(AbiError::ReadOverrun);
        }
        let slice = &self.data[self.pos..self.pos + n];
        self.pos += n;
        Ok(slice)
    }

    fn read_u8(&mut self) -> Result<u8, AbiError> {
        self.read_byte()
    }

    fn read_i8(&mut self) -> Result<i8, AbiError> {
        Ok(self.read_byte()? as i8)
    }

    fn read_u16(&mut self) -> Result<u16, AbiError> {
        let bytes = self.read_bytes(2)?;
        Ok(u16::from_le_bytes([bytes[0], bytes[1]]))
    }

    fn read_i16(&mut self) -> Result<i16, AbiError> {
        let bytes = self.read_bytes(2)?;
        Ok(i16::from_le_bytes([bytes[0], bytes[1]]))
    }

    fn read_u32(&mut self) -> Result<u32, AbiError> {
        let bytes = self.read_bytes(4)?;
        Ok(u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
    }

    fn read_i32(&mut self) -> Result<i32, AbiError> {
        let bytes = self.read_bytes(4)?;
        Ok(i32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
    }

    fn read_u64(&mut self) -> Result<u64, AbiError> {
        let bytes = self.read_bytes(8)?;
        let mut arr = [0u8; 8];
        arr.copy_from_slice(bytes);
        Ok(u64::from_le_bytes(arr))
    }

    fn read_i64(&mut self) -> Result<i64, AbiError> {
        let bytes = self.read_bytes(8)?;
        let mut arr = [0u8; 8];
        arr.copy_from_slice(bytes);
        Ok(i64::from_le_bytes(arr))
    }

    fn read_u128(&mut self) -> Result<u128, AbiError> {
        let bytes = self.read_bytes(16)?;
        let mut arr = [0u8; 16];
        arr.copy_from_slice(bytes);
        Ok(u128::from_le_bytes(arr))
    }

    fn read_i128(&mut self) -> Result<i128, AbiError> {
        let bytes = self.read_bytes(16)?;
        let mut arr = [0u8; 16];
        arr.copy_from_slice(bytes);
        Ok(i128::from_le_bytes(arr))
    }

    fn read_f32(&mut self) -> Result<f32, AbiError> {
        let bytes = self.read_bytes(4)?;
        Ok(f32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
    }

    fn read_f64(&mut self) -> Result<f64, AbiError> {
        let bytes = self.read_bytes(8)?;
        let mut arr = [0u8; 8];
        arr.copy_from_slice(bytes);
        Ok(f64::from_le_bytes(arr))
    }

    fn read_varuint32(&mut self) -> Result<u32, AbiError> {
        let mut val: u32 = 0;
        let mut shift: u32 = 0;
        loop {
            let byte = self.read_byte()?;
            val |= ((byte & 0x7f) as u32) << shift;
            if byte & 0x80 == 0 {
                break;
            }
            shift += 7;
            if shift > 35 {
                return Err(AbiError::InvalidData("varuint32 too large".into()));
            }
        }
        Ok(val)
    }
}

/// Decode EOSIO binary data into a `serde_json::Value` according to the resolved type.
pub fn decode(
    abi: &AbiDef,
    resolved: &ResolvedType,
    reader: &mut BinaryReader,
) -> Result<Value, AbiError> {
    match resolved {
        ResolvedType::Bool => {
            let b = reader.read_u8()?;
            Ok(Value::Bool(b != 0))
        }

        ResolvedType::Int8 => {
            let v = reader.read_i8()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::Int16 => {
            let v = reader.read_i16()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::Int32 => {
            let v = reader.read_i32()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::Int64 => {
            let v = reader.read_i64()?;
            // Use string for i64 to avoid JSON precision loss
            Ok(Value::String(v.to_string()))
        }
        ResolvedType::Int128 => {
            let v = reader.read_i128()?;
            Ok(Value::String(v.to_string()))
        }

        ResolvedType::UInt8 => {
            let v = reader.read_u8()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::UInt16 => {
            let v = reader.read_u16()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::UInt32 => {
            let v = reader.read_u32()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::UInt64 => {
            let v = reader.read_u64()?;
            Ok(Value::String(v.to_string()))
        }
        ResolvedType::UInt128 => {
            let v = reader.read_u128()?;
            Ok(Value::String(v.to_string()))
        }

        ResolvedType::Float32 => {
            let v = reader.read_f32()?;
            Ok(serde_json::Number::from_f64(v as f64)
                .map(Value::Number)
                .unwrap_or(Value::Null))
        }
        ResolvedType::Float64 => {
            let v = reader.read_f64()?;
            Ok(serde_json::Number::from_f64(v)
                .map(Value::Number)
                .unwrap_or(Value::Null))
        }

        ResolvedType::VarUint32 => {
            let v = reader.read_varuint32()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::VarInt32 => {
            let zigzag = reader.read_varuint32()?;
            let v = ((zigzag >> 1) as i32) ^ -((zigzag & 1) as i32);
            Ok(Value::Number(v.into()))
        }

        ResolvedType::Name => {
            let v = reader.read_u64()?;
            Ok(Value::String(crate::u64_to_name(v)))
        }

        ResolvedType::String => {
            let len = reader.read_varuint32()? as usize;
            let bytes = reader.read_bytes(len)?;
            let s = std::str::from_utf8(bytes)
                .map_err(|_| AbiError::InvalidData("invalid UTF-8 in string".into()))?;
            Ok(Value::String(s.to_string()))
        }

        ResolvedType::Bytes => {
            let len = reader.read_varuint32()? as usize;
            let bytes = reader.read_bytes(len)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }

        ResolvedType::Checksum256 => {
            let bytes = reader.read_bytes(32)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }
        ResolvedType::Checksum160 => {
            let bytes = reader.read_bytes(20)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }
        ResolvedType::Checksum512 => {
            let bytes = reader.read_bytes(64)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }

        ResolvedType::TimePoint => {
            let v = reader.read_u64()?;
            Ok(Value::String(v.to_string()))
        }
        ResolvedType::TimePointSec => {
            let v = reader.read_u32()?;
            Ok(Value::Number(v.into()))
        }
        ResolvedType::BlockTimestampType => {
            let v = reader.read_u32()?;
            Ok(Value::Number(v.into()))
        }

        ResolvedType::Asset => {
            let amount = reader.read_i64()?;
            let symbol = reader.read_u64()?;
            Ok(Value::String(crate::format_asset(amount, symbol)))
        }

        ResolvedType::ExtendedAsset => {
            let amount = reader.read_i64()?;
            let symbol = reader.read_u64()?;
            let contract = reader.read_u64()?;
            let mut map = serde_json::Map::new();
            map.insert(
                "quantity".into(),
                Value::String(crate::format_asset(amount, symbol)),
            );
            map.insert(
                "contract".into(),
                Value::String(crate::u64_to_name(contract)),
            );
            Ok(Value::Object(map))
        }

        ResolvedType::Symbol => {
            let raw = reader.read_u64()?;
            Ok(Value::String(crate::format_symbol(raw)))
        }

        ResolvedType::SymbolCode => {
            let raw = reader.read_u64()?;
            Ok(Value::String(crate::u64_to_symbol_code(raw)))
        }

        ResolvedType::PublicKey => {
            // Type byte + 33-byte key = 34 bytes
            let bytes = reader.read_bytes(34)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }

        ResolvedType::Signature => {
            // Type byte + 65-byte sig = 66 bytes
            let bytes = reader.read_bytes(66)?;
            Ok(Value::String(crate::bytes_to_hex(bytes)))
        }

        ResolvedType::Struct(name) => decode_struct(abi, name, reader),

        ResolvedType::Array(inner) => {
            let len = reader.read_varuint32()? as usize;
            let mut arr = Vec::with_capacity(len.min(1024));
            for _ in 0..len {
                arr.push(decode(abi, inner, reader)?);
            }
            Ok(Value::Array(arr))
        }

        ResolvedType::Optional(inner) => {
            let present = reader.read_u8()?;
            if present == 0 {
                Ok(Value::Null)
            } else {
                decode(abi, inner, reader)
            }
        }

        ResolvedType::Variant(name) => decode_variant(abi, name, reader),
    }
}

fn decode_struct(
    abi: &AbiDef,
    struct_name: &str,
    reader: &mut BinaryReader,
) -> Result<Value, AbiError> {
    let st = abi
        .structs
        .iter()
        .find(|s| s.name == struct_name)
        .ok_or_else(|| AbiError::UnknownType(struct_name.to_string()))?;

    let mut map = serde_json::Map::new();

    // Decode base fields first
    if !st.base.is_empty() {
        if let Value::Object(base_map) = decode_struct(abi, &st.base, reader)? {
            for (k, v) in base_map {
                map.insert(k, v);
            }
        }
    }

    // Decode own fields
    for field in &st.fields {
        let resolved = resolve_type(abi, &field.type_)
            .ok_or_else(|| AbiError::UnknownType(field.type_.clone()))?;
        let val = decode(abi, &resolved, reader)?;
        map.insert(field.name.clone(), val);
    }

    Ok(Value::Object(map))
}

fn decode_variant(
    abi: &AbiDef,
    variant_name: &str,
    reader: &mut BinaryReader,
) -> Result<Value, AbiError> {
    let vdef = abi
        .variants
        .iter()
        .find(|v| v.name == variant_name)
        .ok_or_else(|| AbiError::UnknownType(variant_name.to_string()))?;

    let idx = reader.read_varuint32()? as usize;
    if idx >= vdef.types.len() {
        return Err(AbiError::InvalidData(format!(
            "variant index {} out of range for '{}' (has {} types)",
            idx,
            variant_name,
            vdef.types.len()
        )));
    }

    let type_name = &vdef.types[idx];
    let resolved = resolve_type(abi, type_name)
        .ok_or_else(|| AbiError::UnknownType(type_name.clone()))?;
    let val = decode(abi, &resolved, reader)?;

    Ok(Value::Array(vec![
        Value::String(type_name.clone()),
        val,
    ]))
}
