use eosio_signer::*;

// Well-known test key pair (DO NOT use in production — this is a widely-published test key)
const TEST_WIF: &str = "5KQwrPbwdL6PhXujxW37FSSQZ1JiwsST4cqQzDeyXtP79zkvFD3";
const TEST_PUB_LEGACY: &str = "EOS6MRyAjQq8ud7hVNYcfnVPJqcVpscN5So8BhtHuGYqET5GDW5CV";

#[test]
fn test_wif_import_export() {
    let key = PrivateKey::from_wif(TEST_WIF).unwrap();
    // to_wif always exports compressed (prefix "L" or "K"); the well-known dev
    // key uses uncompressed WIF (prefix "5"). Verify the compressed roundtrip.
    let compressed_wif = key.to_wif();
    assert!(compressed_wif.starts_with('K') || compressed_wif.starts_with('L'));
    let key2 = PrivateKey::from_wif(&compressed_wif).unwrap();
    assert_eq!(key.to_bytes(), key2.to_bytes());
}

#[test]
fn test_public_key_derivation() {
    let key = PrivateKey::from_wif(TEST_WIF).unwrap();
    let pub_key = key.public_key();
    let legacy = pub_key.to_legacy_string();
    assert_eq!(legacy, TEST_PUB_LEGACY);
}

#[test]
fn test_public_key_parse_legacy() {
    let pub_key = PublicKey::from_str(TEST_PUB_LEGACY).unwrap();
    let roundtrip = pub_key.to_legacy_string();
    assert_eq!(roundtrip, TEST_PUB_LEGACY);
}

#[test]
fn test_public_key_pub_k1_format() {
    let key = PrivateKey::from_wif(TEST_WIF).unwrap();
    let pub_key = key.public_key();
    let modern = pub_key.to_string();
    assert!(modern.starts_with("PUB_K1_"));

    // Parse it back
    let parsed = PublicKey::from_str(&modern).unwrap();
    assert_eq!(parsed.to_legacy_string(), TEST_PUB_LEGACY);
}

#[test]
fn test_invalid_wif() {
    assert!(PrivateKey::from_wif("notakey").is_err());
    assert!(PrivateKey::from_wif("").is_err());
}

#[test]
fn test_sign_and_verify() {
    let key = PrivateKey::from_wif(TEST_WIF).unwrap();
    let pub_key = key.public_key();

    // Build a minimal transaction
    let tx = Transaction::new(
        TransactionHeader {
            expiration: 1700000000,
            ref_block_num: 100,
            ref_block_prefix: 200,
            max_net_usage_words: 0,
            max_cpu_usage_ms: 0,
            delay_sec: 0,
        },
        vec![],
    );

    // Use WAX testnet chain ID for testing
    let chain_id = "f16b1833c747c43682f4386fca9cbb327929334a762755ebec17f6f23c9b8a12";

    let packed = sign_transaction(&tx, chain_id, &key).unwrap();

    assert_eq!(packed.signatures.len(), 1);
    assert!(packed.signatures[0].starts_with("SIG_K1_"));

    // Verify the signature
    use sha2::{Sha256, Digest};
    let chain_id_bytes = hex::decode(chain_id).unwrap();
    let packed_trx = tx.serialize().unwrap();

    let mut hasher = Sha256::new();
    hasher.update(&chain_id_bytes);
    hasher.update(&packed_trx);
    hasher.update([0u8; 32]);
    let digest: [u8; 32] = hasher.finalize().into();

    assert!(verify_signature(&packed.signatures[0], &digest, &pub_key).unwrap());
}

#[test]
fn test_transaction_serialization() {
    let header = TransactionHeader {
        expiration: 1700000000,
        ref_block_num: 0x1234,
        ref_block_prefix: 0xDEADBEEF,
        max_net_usage_words: 0,
        max_cpu_usage_ms: 0,
        delay_sec: 0,
    };

    let mut action = Action::new(
        "eosio.token",
        "transfer",
        vec![PermissionLevel::active("alice")],
    );
    action.set_data_bytes(vec![0x01, 0x02, 0x03]);

    let tx = Transaction::new(header, vec![action]);
    let serialized = tx.serialize().unwrap();

    // Verify the header bytes
    assert_eq!(&serialized[0..4], &1700000000u32.to_le_bytes());
    assert_eq!(&serialized[4..6], &0x1234u16.to_le_bytes());
    assert_eq!(&serialized[6..10], &0xDEADBEEFu32.to_le_bytes());

    // Should be non-empty (header + varuints + action data)
    assert!(serialized.len() > 10);
}

#[test]
fn test_action_serialization() {
    let mut action = Action::new(
        "eosio.token",
        "transfer",
        vec![
            PermissionLevel::active("alice"),
            PermissionLevel::active("bob"),
        ],
    );
    action.set_data_hex("deadbeef");

    let serialized = action.serialize().unwrap();

    // 8 (account) + 8 (name) + varuint(2) + 2*16 (auth) + varuint(4) + 4 (data)
    assert!(!serialized.is_empty());
    assert!(serialized.len() > 16);
}

#[test]
fn test_permission_level_serialization() {
    let perm = PermissionLevel::active("alice");
    let mut buf = Vec::new();
    perm.serialize(&mut buf);
    assert_eq!(buf.len(), 16); // 8 bytes actor + 8 bytes permission
}

#[test]
fn test_name_encoding() {
    use eosio_signer::transaction::name_to_u64;

    // Known encodings
    let eosio = name_to_u64("eosio");
    assert!(eosio != 0);

    // Empty name
    let empty = name_to_u64("");
    assert_eq!(empty, 0);

    // Max length name
    let long = name_to_u64("abcdefghijklm");
    assert!(long != 0);
}

#[test]
fn test_packed_transaction_format() {
    let packed = PackedTransaction::new(
        vec!["SIG_K1_fake".to_string()],
        vec![0xDE, 0xAD],
    );
    assert_eq!(packed.compression, 0);
    assert_eq!(packed.packed_trx, "dead");
    assert_eq!(packed.signatures.len(), 1);
}
