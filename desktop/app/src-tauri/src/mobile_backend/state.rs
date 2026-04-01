use std::path::PathBuf;
use std::sync::{Arc, Mutex};

use super::crypto::{WalletData, WalletFile};

/// Shared application state for the embedded backend.
#[derive(Clone)]
pub struct AppState {
    pub inner: Arc<Mutex<Inner>>,
}

pub struct Inner {
    /// Bearer token generated at startup.
    pub token: String,
    /// Password used to encrypt/decrypt the wallet (kept while unlocked).
    pub password: Option<String>,
    /// Decrypted wallet data (None = locked).
    pub wallet_data: Option<WalletData>,
    /// Path to the encrypted wallet file on disk.
    pub wallet_path: PathBuf,
    /// Active network chain ID.
    pub active_chain_id: String,
    /// Active account name.
    pub active_account: Option<String>,
    /// Active account authority.
    pub active_authority: Option<String>,
}

impl AppState {
    pub fn new(wallet_path: PathBuf) -> Self {
        let token = generate_token();
        println!("[mobile-backend] BACKEND_TOKEN={token}");

        Self {
            inner: Arc::new(Mutex::new(Inner {
                token,
                password: None,
                wallet_data: None,
                wallet_path,
                // Default to WAX
                active_chain_id: "1064487b3cd1a897ce03ae5b6a865651747e2e152090f99c1d19d44e01aea5a4"
                    .to_string(),
                active_account: None,
                active_authority: None,
            })),
        }
    }

    /// Check whether the encrypted wallet file exists on disk.
    pub fn wallet_file_exists(&self) -> bool {
        let inner = self.inner.lock().unwrap();
        inner.wallet_path.exists()
    }

    /// Check whether the wallet is currently unlocked in memory.
    pub fn wallet_unlocked(&self) -> bool {
        let inner = self.inner.lock().unwrap();
        inner.wallet_data.is_some()
    }

    /// Get the bearer token.
    pub fn token(&self) -> String {
        self.inner.lock().unwrap().token.clone()
    }

    /// Create a new empty wallet, encrypt it, and write to disk.
    pub fn create_wallet(&self, password: &str) -> Result<(), String> {
        let data = WalletData::default();
        let file = WalletFile::encrypt(password, &data)?;
        let json = serde_json::to_string_pretty(&file)
            .map_err(|e| format!("serialize wallet: {e}"))?;

        let mut inner = self.inner.lock().unwrap();
        std::fs::write(&inner.wallet_path, json)
            .map_err(|e| format!("write wallet file: {e}"))?;
        inner.wallet_data = Some(data);
        inner.password = Some(password.to_string());
        Ok(())
    }

    /// Unlock an existing wallet file with the given password.
    pub fn unlock(&self, password: &str) -> Result<bool, String> {
        let mut inner = self.inner.lock().unwrap();

        let json = std::fs::read_to_string(&inner.wallet_path)
            .map_err(|e| format!("read wallet file: {e}"))?;
        let file: WalletFile = serde_json::from_str(&json)
            .map_err(|e| format!("parse wallet file: {e}"))?;

        match file.decrypt(password) {
            Ok(data) => {
                inner.wallet_data = Some(data);
                inner.password = Some(password.to_string());
                Ok(true)
            }
            Err(_) => Ok(false), // wrong password
        }
    }

    /// Lock the wallet (clear plaintext data from memory).
    pub fn lock(&self) {
        let mut inner = self.inner.lock().unwrap();
        inner.wallet_data = None;
        inner.password = None;
    }

    /// Save current wallet data back to the encrypted file.
    pub fn save(&self) -> Result<(), String> {
        let inner = self.inner.lock().unwrap();
        let password = inner.password.as_deref().ok_or("wallet is locked")?;
        let data = inner.wallet_data.as_ref().ok_or("no wallet data")?;

        let file = WalletFile::encrypt(password, data)?;
        let json = serde_json::to_string_pretty(&file)
            .map_err(|e| format!("serialize wallet: {e}"))?;
        std::fs::write(&inner.wallet_path, json)
            .map_err(|e| format!("write wallet file: {e}"))?;
        Ok(())
    }
}

fn generate_token() -> String {
    let bytes: [u8; 32] = rand::random();
    hex::encode(bytes)
}
