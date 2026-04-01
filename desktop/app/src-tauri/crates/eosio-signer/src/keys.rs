use sha2::{Sha256, Digest};
use ripemd::Ripemd160;
use k256::ecdsa::{SigningKey, VerifyingKey};
#[allow(unused_imports)]
use k256::elliptic_curve::sec1::ToEncodedPoint;

/// An EOSIO private key (K1 / secp256k1).
#[derive(Clone)]
pub struct PrivateKey {
    inner: SigningKey,
}

/// An EOSIO public key (K1 / secp256k1).
#[derive(Clone)]
pub struct PublicKey {
    inner: VerifyingKey,
}

impl PrivateKey {
    /// Import from WIF (Wallet Import Format) string.
    ///
    /// WIF format: Base58Check with 0x80 prefix + 32-byte key [+ 0x01 if compressed].
    pub fn from_wif(wif: &str) -> Result<Self, crate::SignerError> {
        let decoded = bs58::decode(wif)
            .into_vec()
            .map_err(|e| crate::SignerError::InvalidKey(format!("base58 decode: {}", e)))?;

        if decoded.len() < 37 {
            return Err(crate::SignerError::InvalidKey(
                "WIF too short".into(),
            ));
        }

        // Verify checksum (last 4 bytes = first 4 bytes of double-SHA256 of payload)
        let payload_end = decoded.len() - 4;
        let payload = &decoded[..payload_end];
        let checksum = &decoded[payload_end..];

        let hash1 = Sha256::digest(payload);
        let hash2 = Sha256::digest(hash1);

        if &hash2[..4] != checksum {
            return Err(crate::SignerError::InvalidKey(
                "WIF checksum mismatch".into(),
            ));
        }

        // First byte should be 0x80
        if payload[0] != 0x80 {
            return Err(crate::SignerError::InvalidKey(
                format!("unexpected WIF version byte: 0x{:02x}", payload[0]),
            ));
        }

        // Key bytes: skip version byte, optionally skip compression flag
        let key_bytes = if payload.len() == 34 {
            // 1 (version) + 32 (key) + 1 (compression flag)
            &payload[1..33]
        } else if payload.len() == 33 {
            // 1 (version) + 32 (key)
            &payload[1..33]
        } else {
            return Err(crate::SignerError::InvalidKey(
                format!("unexpected WIF payload length: {}", payload.len()),
            ));
        };

        let signing_key = SigningKey::from_bytes(key_bytes.into())
            .map_err(|e| crate::SignerError::InvalidKey(format!("invalid key bytes: {}", e)))?;

        Ok(Self { inner: signing_key })
    }

    /// Export to WIF format (compressed).
    pub fn to_wif(&self) -> String {
        let key_bytes = self.inner.to_bytes();
        // 0x80 + key + 0x01 (compressed)
        let mut payload = Vec::with_capacity(34);
        payload.push(0x80);
        payload.extend_from_slice(&key_bytes);
        payload.push(0x01); // compressed flag

        let hash1 = Sha256::digest(&payload);
        let hash2 = Sha256::digest(hash1);
        payload.extend_from_slice(&hash2[..4]);

        bs58::encode(payload).into_string()
    }

    /// Create from raw 32-byte key.
    pub fn from_bytes(bytes: &[u8]) -> Result<Self, crate::SignerError> {
        let signing_key = SigningKey::from_bytes(bytes.into())
            .map_err(|e| crate::SignerError::InvalidKey(format!("invalid key bytes: {}", e)))?;
        Ok(Self { inner: signing_key })
    }

    /// Get the corresponding public key.
    pub fn public_key(&self) -> PublicKey {
        PublicKey {
            inner: *self.inner.verifying_key(),
        }
    }

    /// Get a reference to the underlying k256 signing key.
    pub(crate) fn signing_key(&self) -> &SigningKey {
        &self.inner
    }

    /// Get raw 32-byte key material.
    pub fn to_bytes(&self) -> Vec<u8> {
        self.inner.to_bytes().to_vec()
    }
}

impl PublicKey {
    /// Parse a legacy EOSIO public key string (e.g., `"EOS6MRy..."`).
    pub fn from_str(s: &str) -> Result<Self, crate::SignerError> {
        let (prefix, body) = if let Some(rest) = s.strip_prefix("PUB_K1_") {
            ("PUB_K1_", rest)
        } else if let Some(rest) = s.strip_prefix("EOS") {
            ("EOS", rest)
        } else {
            return Err(crate::SignerError::InvalidKey(
                "public key must start with EOS or PUB_K1_".into(),
            ));
        };

        let decoded = bs58::decode(body)
            .into_vec()
            .map_err(|e| crate::SignerError::InvalidKey(format!("base58 decode: {}", e)))?;

        if decoded.len() < 37 {
            return Err(crate::SignerError::InvalidKey(
                "public key data too short".into(),
            ));
        }

        let key_data = &decoded[..33];
        let checksum = &decoded[33..37];

        // Verify checksum
        let expected = if prefix == "PUB_K1_" {
            // PUB_K1_ uses RIPEMD160(key + "K1")
            let mut hasher = Ripemd160::new();
            hasher.update(key_data);
            hasher.update(b"K1");
            let hash = hasher.finalize();
            [hash[0], hash[1], hash[2], hash[3]]
        } else {
            // Legacy "EOS" uses RIPEMD160(key)
            let mut hasher = Ripemd160::new();
            hasher.update(key_data);
            let hash = hasher.finalize();
            [hash[0], hash[1], hash[2], hash[3]]
        };

        if checksum != expected {
            return Err(crate::SignerError::InvalidKey(
                "public key checksum mismatch".into(),
            ));
        }

        let verifying_key = VerifyingKey::from_sec1_bytes(key_data)
            .map_err(|e| crate::SignerError::InvalidKey(format!("invalid public key: {}", e)))?;

        Ok(Self { inner: verifying_key })
    }

    /// Format as legacy `"EOS..."` string.
    pub fn to_legacy_string(&self) -> String {
        let point = self.inner.to_encoded_point(true);
        let key_data = point.as_bytes();

        let mut hasher = Ripemd160::new();
        hasher.update(key_data);
        let hash = hasher.finalize();

        let mut payload = Vec::with_capacity(37);
        payload.extend_from_slice(key_data);
        payload.extend_from_slice(&hash[..4]);

        format!("EOS{}", bs58::encode(payload).into_string())
    }

    /// Format as modern `"PUB_K1_..."` string.
    pub fn to_string(&self) -> String {
        let point = self.inner.to_encoded_point(true);
        let key_data = point.as_bytes();

        let mut hasher = Ripemd160::new();
        hasher.update(key_data);
        hasher.update(b"K1");
        let hash = hasher.finalize();

        let mut payload = Vec::with_capacity(37);
        payload.extend_from_slice(key_data);
        payload.extend_from_slice(&hash[..4]);

        format!("PUB_K1_{}", bs58::encode(payload).into_string())
    }

    /// Get the raw 33-byte compressed public key.
    pub fn to_bytes(&self) -> Vec<u8> {
        let point = self.inner.to_encoded_point(true);
        point.as_bytes().to_vec()
    }

    /// Get a reference to the underlying k256 verifying key.
    pub(crate) fn verifying_key(&self) -> &VerifyingKey {
        &self.inner
    }
}

impl std::fmt::Display for PublicKey {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.to_legacy_string())
    }
}

impl std::fmt::Debug for PublicKey {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "PublicKey({})", self.to_legacy_string())
    }
}

impl std::fmt::Debug for PrivateKey {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "PrivateKey(***)")
    }
}
