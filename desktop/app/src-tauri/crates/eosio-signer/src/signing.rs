use k256::ecdsa::{Signature, RecoveryId, VerifyingKey};
use k256::ecdsa::hazmat::SignPrimitive;
use sha2::{Sha256, Digest};
use ripemd::Ripemd160;

use crate::keys::PrivateKey;
use crate::transaction::{Transaction, PackedTransaction};

/// The chain ID is prepended to the serialized transaction before signing.
/// This prevents replay attacks across chains.
pub fn sign_transaction(
    tx: &Transaction,
    chain_id: &str,
    private_key: &PrivateKey,
) -> Result<PackedTransaction, crate::SignerError> {
    let packed_trx = tx.serialize()?;
    let signature = sign_bytes(&packed_trx, chain_id, private_key)?;
    Ok(PackedTransaction::new(vec![signature], packed_trx))
}

/// Sign a serialized transaction and return the EOSIO signature string.
///
/// The signing digest is: SHA256(chain_id_bytes + serialized_trx + context_free_data_hash)
pub fn sign_bytes(
    packed_trx: &[u8],
    chain_id: &str,
    private_key: &PrivateKey,
) -> Result<String, crate::SignerError> {
    let chain_id_bytes = hex::decode(chain_id)
        .map_err(|e| crate::SignerError::SerializationError(format!("invalid chain_id hex: {}", e)))?;

    // Build the signing digest: SHA256(chain_id + packed_trx + 32-byte zero hash for context-free data)
    let mut hasher = Sha256::new();
    hasher.update(&chain_id_bytes);
    hasher.update(packed_trx);
    hasher.update([0u8; 32]); // empty context-free data digest
    let digest: [u8; 32] = hasher.finalize().into();

    // Sign with canonical recovery
    let (sig, recid) = sign_canonical(private_key, &digest)?;

    // Encode as EOSIO SIG_K1_ format
    let sig_string = encode_signature_k1(&sig, recid);
    Ok(sig_string)
}

/// Check if a signature meets EOSIO's `is_canonical` requirements.
///
/// Compact layout: `[R (32 bytes), S (32 bytes)]`
///
/// Both R and S must be positive (high bit clear) and must not carry
/// an unnecessary leading zero byte.
fn is_canonical(sig_bytes: &[u8; 64]) -> bool {
    let r = &sig_bytes[0..32];
    let s = &sig_bytes[32..64];

    (r[0] & 0x80) == 0               // R is positive
        && !(r[0] == 0 && (r[1] & 0x80) == 0)   // R not over-padded
        && (s[0] & 0x80) == 0               // S is positive
        && !(s[0] == 0 && (s[1] & 0x80) == 0)   // S not over-padded
}

/// Find the correct recovery ID by trial — try all 4 IDs and pick the one
/// that recovers to our public key. This matches the eos-sharp approach and
/// avoids relying on the library's recovery ID after S-normalization.
fn find_recovery_id(
    signature: &Signature,
    digest: &[u8; 32],
    expected_pub: &VerifyingKey,
) -> Option<u8> {
    for id in 0u8..4 {
        if let Some(recid) = RecoveryId::from_byte(id) {
            if let Ok(recovered) = VerifyingKey::recover_from_prehash(digest, signature, recid) {
                if &recovered == expected_pub {
                    return Some(id);
                }
            }
        }
    }
    None
}

/// Sign a digest, retrying with different RFC 6979 additional data until
/// the result passes EOSIO's `is_canonical` check.
fn sign_canonical(
    private_key: &PrivateKey,
    digest: &[u8; 32],
) -> Result<([u8; 64], u8), crate::SignerError> {
    let signing_key = private_key.signing_key();
    let expected_pub = signing_key.verifying_key();
    let scalar = signing_key.as_nonzero_scalar();
    let z = k256::FieldBytes::from(*digest);

    for nonce in 0u32..256 {
        let ad = nonce.to_le_bytes();
        let ad_slice: &[u8] = if nonce == 0 { &[] } else { &ad };

        let (signature, _): (Signature, Option<RecoveryId>) = scalar
            .try_sign_prehashed_rfc6979::<Sha256>(&z, ad_slice)
            .map_err(|e| crate::SignerError::SigningError(format!("ECDSA sign failed: {}", e)))?;

        let sig_bytes: [u8; 64] = signature.to_bytes().into();

        if !is_canonical(&sig_bytes) {
            continue;
        }

        // Find the correct recovery ID by actually recovering the public key
        if let Some(recid) = find_recovery_id(&signature, digest, expected_pub) {
            return Ok((sig_bytes, recid));
        }
    }

    Err(crate::SignerError::SigningError(
        "failed to produce canonical signature after 256 attempts".into(),
    ))
}

/// Encode a signature as `SIG_K1_...` (base58 with RIPEMD160 checksum).
fn encode_signature_k1(sig_bytes: &[u8; 64], recovery_id: u8) -> String {
    // Format: i (1 byte, 31+recovery_id) + r (32 bytes) + s (32 bytes) = 65 bytes
    let mut compact = Vec::with_capacity(65);
    compact.push(recovery_id + 31); // EOSIO adds 31 to recovery_id for the header byte
    compact.extend_from_slice(sig_bytes);

    // Checksum: RIPEMD160(compact + "K1")
    let mut hasher = Ripemd160::new();
    hasher.update(&compact);
    hasher.update(b"K1");
    let hash = hasher.finalize();

    let mut payload = Vec::with_capacity(69);
    payload.extend_from_slice(&compact);
    payload.extend_from_slice(&hash[..4]);

    format!("SIG_K1_{}", bs58::encode(payload).into_string())
}

/// Verify that a SIG_K1_ signature is valid for the given digest and public key.
pub fn verify_signature(
    signature: &str,
    digest: &[u8; 32],
    public_key: &crate::keys::PublicKey,
) -> Result<bool, crate::SignerError> {
    let sig_data = decode_signature_k1(signature)?;

    let recovery_id = sig_data[0].checked_sub(31)
        .ok_or_else(|| crate::SignerError::InvalidKey("invalid signature header byte".into()))?;

    let mut sig_bytes = [0u8; 64];
    sig_bytes.copy_from_slice(&sig_data[1..65]);

    let signature = Signature::from_bytes((&sig_bytes).into())
        .map_err(|e| crate::SignerError::InvalidKey(format!("invalid signature: {}", e)))?;

    let recid = RecoveryId::from_byte(recovery_id)
        .ok_or_else(|| crate::SignerError::InvalidKey("invalid recovery ID".into()))?;

    // Recover the public key and compare
    let recovered = k256::ecdsa::VerifyingKey::recover_from_prehash(digest, &signature, recid)
        .map_err(|e| crate::SignerError::SigningError(format!("recovery failed: {}", e)))?;

    Ok(&recovered == public_key.verifying_key())
}

/// Decode a `SIG_K1_...` string into the raw 65-byte data.
fn decode_signature_k1(sig: &str) -> Result<Vec<u8>, crate::SignerError> {
    let body = sig
        .strip_prefix("SIG_K1_")
        .ok_or_else(|| crate::SignerError::InvalidKey("signature must start with SIG_K1_".into()))?;

    let decoded = bs58::decode(body)
        .into_vec()
        .map_err(|e| crate::SignerError::InvalidKey(format!("base58 decode: {}", e)))?;

    if decoded.len() != 69 {
        return Err(crate::SignerError::InvalidKey(
            format!("expected 69 bytes in signature, got {}", decoded.len()),
        ));
    }

    // Verify checksum
    let data = &decoded[..65];
    let checksum = &decoded[65..69];

    let mut hasher = Ripemd160::new();
    hasher.update(data);
    hasher.update(b"K1");
    let hash = hasher.finalize();

    if &hash[..4] != checksum {
        return Err(crate::SignerError::InvalidKey(
            "signature checksum mismatch".into(),
        ));
    }

    Ok(data.to_vec())
}
