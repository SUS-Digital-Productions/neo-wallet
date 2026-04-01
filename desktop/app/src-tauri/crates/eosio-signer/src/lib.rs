//! # eosio-signer
//!
//! EOSIO/WAX transaction signing library.
//!
//! Provides private key management (WIF import/export), transaction building,
//! ECDSA signing (secp256k1/K1), and a blocking chain API client.
//!
//! # Example
//!
//! ```rust,no_run
//! use eosio_signer::{PrivateKey, ChainApi, Action, PermissionLevel};
//!
//! // Import your private key from WIF
//! let key = PrivateKey::from_wif("5K...").unwrap();
//! println!("Public key: {}", key.public_key());
//!
//! // Connect to chain
//! let api = ChainApi::new("https://testnet.waxsweden.org");
//!
//! // Build an action
//! let mut action = Action::new(
//!     "eosio.token",
//!     "transfer",
//!     vec![PermissionLevel::active("myaccount")],
//! );
//! action.set_data_bytes(vec![]); // Use eosio-abi to encode action data
//!
//! // Sign and push
//! let result = api.transact(vec![action], &key, 120).unwrap();
//! println!("Transaction ID: {}", result.transaction_id);
//! ```

#[cfg(feature = "api")]
pub mod api;
pub mod keys;
pub mod signing;
pub mod transaction;

#[cfg(feature = "api")]
pub use api::{ChainApi, ChainInfo, BlockInfo, PushTransactionResponse, TableRowsResponse, AccountInfo};
pub use keys::{PrivateKey, PublicKey};
pub use signing::{sign_transaction, sign_bytes, verify_signature};
pub use transaction::{Action, PermissionLevel, Transaction, TransactionHeader, PackedTransaction};

use std::fmt;

/// Errors from the signer library.
#[derive(Debug)]
pub enum SignerError {
    /// Invalid key format or data.
    InvalidKey(String),
    /// Serialization failure.
    SerializationError(String),
    /// ECDSA signing failure.
    SigningError(String),
    /// HTTP/network error.
    NetworkError(String),
    /// Chain API returned an error.
    ChainError(String),
}

impl fmt::Display for SignerError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SignerError::InvalidKey(s) => write!(f, "invalid key: {}", s),
            SignerError::SerializationError(s) => write!(f, "serialization error: {}", s),
            SignerError::SigningError(s) => write!(f, "signing error: {}", s),
            SignerError::NetworkError(s) => write!(f, "network error: {}", s),
            SignerError::ChainError(s) => write!(f, "chain error: {}", s),
        }
    }
}

impl std::error::Error for SignerError {}
