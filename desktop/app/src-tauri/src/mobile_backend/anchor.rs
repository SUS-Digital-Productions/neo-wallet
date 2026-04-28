// Anchor / Greymass wallet backup importer (mobile).
//
// Best-effort decryptor for foreign wallet backup files. Different tools wrap
// AES-CBC ciphertext in slightly different JSON envelopes and use different
// KDFs / iteration counts. We try every reasonable combination until one
// yields plaintext containing recognisable EOSIO WIF private keys.
//
// Mirrors `desktop/backend/Services/AnchorWalletImporter.cs` so an Anchor
// backup that imports successfully on desktop will also import on mobile.

use aes::cipher::{block_padding::Pkcs7, BlockDecryptMut, KeyIvInit};
use base64::{engine::general_purpose::STANDARD as B64, Engine};
use eosio_signer::PrivateKey as EosioPrivateKey;
use pbkdf2::pbkdf2_hmac;
use regex::Regex;
use scrypt::{scrypt, Params as ScryptParams};
use serde_json::Value;
use sha2::{Sha256, Sha512};
use std::sync::OnceLock;

type Aes256CbcDec = cbc::Decryptor<aes::Aes256>;

pub struct AnchorImportResult {
    pub format: String,
    pub keys_wif: Vec<String>,
}

pub fn try_import(file_bytes: &[u8], password: &str) -> Option<AnchorImportResult> {
    let text = std::str::from_utf8(file_bytes).ok()?.trim();
    let envelope = extract_envelope(text)?;
    let (salt, iv, cipher, format) = envelope;

    const KDFS: &[&str] = &[
        "pbkdf2-sha512-100k",
        "pbkdf2-sha256-100k",
        "pbkdf2-sha512-10k",
        "pbkdf2-sha256-10k",
        "scrypt-16384-8-1",
    ];

    for kdf in KDFS {
        let key = match derive_key(kdf, password, &salt) {
            Some(k) => k,
            None => continue,
        };
        let plaintext = match aes_cbc_decrypt(&cipher, &key, &iv) {
            Some(pt) => pt,
            None => continue,
        };
        let keys = extract_private_keys(&plaintext);
        if !keys.is_empty() {
            return Some(AnchorImportResult {
                format: format!("{format} + {kdf}"),
                keys_wif: keys,
            });
        }
    }

    None
}

// ----------------------------------------------------------------------
// Envelope detection
// ----------------------------------------------------------------------

fn extract_envelope(text: &str) -> Option<(Vec<u8>, Vec<u8>, Vec<u8>, String)> {
    // JSON envelope first.
    if let Ok(value) = serde_json::from_str::<Value>(text) {
        if let Some(obj) = value.as_object() {
            for inner in ["wallet", "data", "encrypted"] {
                if let Some(sub) = obj.get(inner).and_then(|v| v.as_object()) {
                    if let Some((s, i, c)) = read_fields(sub) {
                        return Some((s, i, c, format!("json:{inner}")));
                    }
                }
            }
            if let Some((s, i, c)) = read_fields(obj) {
                return Some((s, i, c, "json:root".to_string()));
            }
        }
    }

    // Concatenated blob: salt[16] | iv[16] | ciphertext.
    if let Some(blob) = try_decode_bytes(text) {
        if blob.len() > 32 {
            let salt = blob[..16].to_vec();
            let iv = blob[16..32].to_vec();
            let cipher = blob[32..].to_vec();
            return Some((salt, iv, cipher, "blob:concat".to_string()));
        }
    }

    None
}

fn read_fields(
    obj: &serde_json::Map<String, Value>,
) -> Option<(Vec<u8>, Vec<u8>, Vec<u8>)> {
    const SALT_NAMES: &[&str] = &["salt", "s"];
    const IV_NAMES: &[&str] = &["iv", "IV", "nonce"];
    const CIPHER_NAMES: &[&str] = &["data", "ciphertext", "ct", "encrypted", "value"];

    let salt = read_first(obj, SALT_NAMES)?;
    let iv = read_first(obj, IV_NAMES)?;
    let cipher = read_first(obj, CIPHER_NAMES)?;
    Some((salt, iv, cipher))
}

fn read_first(obj: &serde_json::Map<String, Value>, names: &[&str]) -> Option<Vec<u8>> {
    for n in names {
        if let Some(s) = obj.get(*n).and_then(|v| v.as_str()) {
            if let Some(b) = try_decode_bytes(s) {
                return Some(b);
            }
        }
    }
    None
}

fn try_decode_bytes(s: &str) -> Option<Vec<u8>> {
    let s = s.trim().trim_matches('"');
    // Hex.
    if s.len() % 2 == 0 && s.chars().all(|c| c.is_ascii_hexdigit()) {
        if let Ok(b) = hex::decode(s) {
            return Some(b);
        }
    }
    // Base64.
    B64.decode(s).ok()
}

// ----------------------------------------------------------------------
// KDFs
// ----------------------------------------------------------------------

fn derive_key(kdf: &str, password: &str, salt: &[u8]) -> Option<[u8; 32]> {
    let mut key = [0u8; 32];
    let pw = password.as_bytes();
    match kdf {
        "pbkdf2-sha256-100k" => {
            pbkdf2_hmac::<Sha256>(pw, salt, 100_000, &mut key);
        }
        "pbkdf2-sha256-10k" => {
            pbkdf2_hmac::<Sha256>(pw, salt, 10_000, &mut key);
        }
        "pbkdf2-sha512-100k" => {
            pbkdf2_hmac::<Sha512>(pw, salt, 100_000, &mut key);
        }
        "pbkdf2-sha512-10k" => {
            pbkdf2_hmac::<Sha512>(pw, salt, 10_000, &mut key);
        }
        "scrypt-16384-8-1" => {
            // log_n=14 → N=16384.
            let params = ScryptParams::new(14, 8, 1, 32).ok()?;
            scrypt(pw, salt, &params, &mut key).ok()?;
        }
        _ => return None,
    }
    Some(key)
}

// ----------------------------------------------------------------------
// AES-256-CBC
// ----------------------------------------------------------------------

fn aes_cbc_decrypt(cipher: &[u8], key: &[u8; 32], iv: &[u8]) -> Option<Vec<u8>> {
    if iv.len() != 16 {
        return None;
    }
    let mut iv_arr = [0u8; 16];
    iv_arr.copy_from_slice(iv);
    let dec = Aes256CbcDec::new(key.into(), &iv_arr.into());
    let mut buf = cipher.to_vec();
    dec.decrypt_padded_mut::<Pkcs7>(&mut buf)
        .ok()
        .map(|pt| pt.to_vec())
}

// ----------------------------------------------------------------------
// Private-key extraction
// ----------------------------------------------------------------------

static WIF_REGEX: OnceLock<Regex> = OnceLock::new();

fn wif_regex() -> &'static Regex {
    WIF_REGEX.get_or_init(|| {
        Regex::new(r"\b[5KL][1-9A-HJ-NP-Za-km-z]{50,51}\b").expect("WIF regex compiles")
    })
}

fn extract_private_keys(plaintext: &[u8]) -> Vec<String> {
    let text = match std::str::from_utf8(plaintext) {
        Ok(s) => s,
        Err(_) => return Vec::new(),
    };

    let mut found: Vec<String> = Vec::new();
    for m in wif_regex().find_iter(text) {
        let candidate = m.as_str();
        if EosioPrivateKey::from_wif(candidate).is_ok() && !found.iter().any(|w| w == candidate) {
            found.push(candidate.to_string());
        }
    }
    found
}
