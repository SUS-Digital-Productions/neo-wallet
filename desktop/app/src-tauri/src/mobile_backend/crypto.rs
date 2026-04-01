// Wallet file encryption/decryption using AES-256-CBC with PBKDF2-SHA256.
// The format is byte-compatible with the .NET desktop backend so wallet files
// can be transferred between desktop and mobile.

use aes::cipher::{block_padding::Pkcs7, BlockEncryptMut, BlockDecryptMut, KeyIvInit};
use base64::{engine::general_purpose::STANDARD as B64, Engine};
use pbkdf2::pbkdf2_hmac;
use serde::{Deserialize, Serialize};
use sha2::Sha256;
use std::convert::TryInto;

const SALT_SIZE: usize = 16;
const KEY_SIZE: usize = 32; // AES-256
const IV_SIZE: usize = 16;
const ITERATIONS: u32 = 100_000;

type Aes256CbcEnc = cbc::Encryptor<aes::Aes256>;
type Aes256CbcDec = cbc::Decryptor<aes::Aes256>;

/// On-disk encrypted wallet file (JSON).
#[derive(Debug, Serialize, Deserialize)]
pub struct WalletFile {
    pub version: u32,
    pub salt: String,
    pub iv: String,
    pub ciphertext: String,
}

/// Plaintext wallet data held in memory while unlocked.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct WalletData {
    pub accounts: Vec<WalletAccount>,
    #[serde(default)]
    pub keys: Vec<WalletKey>,
}

/// Account entry in the wallet.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WalletAccount {
    pub account: String,
    #[serde(default = "default_authority")]
    pub authority: String,
    #[serde(rename = "privateKeyWif")]
    pub private_key_wif: String,
    #[serde(rename = "publicKey")]
    pub public_key: String,
    #[serde(rename = "chainId")]
    pub chain_id: String,
}

fn default_authority() -> String {
    "active".to_string()
}

/// Standalone key entry in the wallet.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WalletKey {
    pub label: String,
    #[serde(rename = "privateKeyWif")]
    pub private_key_wif: String,
    #[serde(rename = "publicKey")]
    pub public_key: String,
}

impl WalletFile {
    /// Encrypt wallet data with the given password and return an on-disk file.
    pub fn encrypt(password: &str, data: &WalletData) -> Result<Self, String> {
        let salt: [u8; SALT_SIZE] = rand::random();
        let iv: [u8; IV_SIZE] = rand::random();

        let key = derive_key(password, &salt);
        let plaintext = serde_json::to_vec(data)
            .map_err(|e| format!("serialize wallet data: {e}"))?;

        let ciphertext = encrypt_aes256_cbc(&key, &iv, &plaintext);

        Ok(Self {
            version: 1,
            salt: B64.encode(salt),
            iv: B64.encode(iv),
            ciphertext: B64.encode(ciphertext),
        })
    }

    /// Decrypt the on-disk file with the given password.
    pub fn decrypt(&self, password: &str) -> Result<WalletData, String> {
        let salt = B64.decode(&self.salt).map_err(|e| format!("decode salt: {e}"))?;
        let iv = B64.decode(&self.iv).map_err(|e| format!("decode iv: {e}"))?;
        let ciphertext = B64
            .decode(&self.ciphertext)
            .map_err(|e| format!("decode ciphertext: {e}"))?;

        let key = derive_key(password, &salt);
        let iv_arr: [u8; IV_SIZE] = iv
            .try_into()
            .map_err(|_| "invalid IV length".to_string())?;
        let plaintext = decrypt_aes256_cbc(&key, &iv_arr, &ciphertext)?;

        serde_json::from_slice(&plaintext)
            .map_err(|e| format!("deserialize wallet data: {e}"))
    }
}

fn derive_key(password: &str, salt: &[u8]) -> [u8; KEY_SIZE] {
    let mut key = [0u8; KEY_SIZE];
    pbkdf2_hmac::<Sha256>(password.as_bytes(), salt, ITERATIONS, &mut key);
    key
}

fn encrypt_aes256_cbc(key: &[u8; KEY_SIZE], iv: &[u8; IV_SIZE], plaintext: &[u8]) -> Vec<u8> {
    let enc = Aes256CbcEnc::new(key.into(), iv.into());
    // Allocate buffer: plaintext + up to one full block of PKCS7 padding
    let mut buf = vec![0u8; plaintext.len() + 16];
    buf[..plaintext.len()].copy_from_slice(plaintext);
    let ct = enc
        .encrypt_padded_mut::<Pkcs7>(&mut buf, plaintext.len())
        .expect("encrypt buffer too small");
    ct.to_vec()
}

fn decrypt_aes256_cbc(
    key: &[u8; KEY_SIZE],
    iv: &[u8; IV_SIZE],
    ciphertext: &[u8],
) -> Result<Vec<u8>, String> {
    let dec = Aes256CbcDec::new(key.into(), iv.into());
    let mut buf = ciphertext.to_vec();
    let pt = dec
        .decrypt_padded_mut::<Pkcs7>(&mut buf)
        .map_err(|_| "decryption failed (wrong password?)".to_string())?;
    Ok(pt.to_vec())
}
