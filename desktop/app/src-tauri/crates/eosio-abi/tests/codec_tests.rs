use eosio_abi::*;
use serde_json::json;

fn make_token_abi() -> AbiCodec {
    let abi_json = r#"{
        "version": "eosio::abi/1.2",
        "types": [
            {"new_type_name": "account_name", "type": "name"}
        ],
        "structs": [
            {
                "name": "transfer",
                "base": "",
                "fields": [
                    {"name": "from", "type": "name"},
                    {"name": "to", "type": "name"},
                    {"name": "quantity", "type": "asset"},
                    {"name": "memo", "type": "string"}
                ]
            },
            {
                "name": "create",
                "base": "",
                "fields": [
                    {"name": "issuer", "type": "name"},
                    {"name": "maximum_supply", "type": "asset"}
                ]
            },
            {
                "name": "account",
                "base": "",
                "fields": [
                    {"name": "balance", "type": "asset"}
                ]
            },
            {
                "name": "currency_stats",
                "base": "",
                "fields": [
                    {"name": "supply", "type": "asset"},
                    {"name": "max_supply", "type": "asset"},
                    {"name": "issuer", "type": "name"}
                ]
            }
        ],
        "actions": [
            {"name": "transfer", "type": "transfer", "ricardian_contract": ""},
            {"name": "create", "type": "create", "ricardian_contract": ""}
        ],
        "tables": [
            {"name": "accounts", "type": "account", "index_type": "i64"},
            {"name": "stat", "type": "currency_stats", "index_type": "i64"}
        ],
        "variants": []
    }"#;
    AbiCodec::from_json(abi_json).unwrap()
}

#[test]
fn test_name_roundtrip() {
    let names = ["eosio", "alice", "bob", "eosio.token", "test.pasha", "a"];
    for name in &names {
        let encoded = name_to_u64(name);
        let decoded = u64_to_name(encoded);
        assert_eq!(&decoded, *name, "name roundtrip failed for '{}'", name);
    }
}

#[test]
fn test_asset_roundtrip() {
    let cases = [
        ("1.0000 WAX", 10000i64),
        ("0.0001 WAX", 1),
        ("100.0000 WAX", 1000000),
        ("-5.0000 EOS", -50000),
        ("0.00000000 BTC", 0),
    ];
    for (s, expected_amount) in &cases {
        let (amount, symbol) = parse_asset(s).unwrap();
        assert_eq!(amount, *expected_amount, "amount mismatch for '{}'", s);
        let formatted = format_asset(amount, symbol);
        assert_eq!(&formatted, s, "format roundtrip failed for '{}'", s);
    }
}

#[test]
fn test_symbol_roundtrip() {
    let cases = ["4,WAX", "8,BTC", "0,USDT", "6,EOS"];
    for s in &cases {
        let raw = parse_symbol(s).unwrap();
        let formatted = format_symbol(raw);
        assert_eq!(&formatted, s, "symbol roundtrip failed for '{}'", s);
    }
}

#[test]
fn test_hex_roundtrip() {
    let bytes = vec![0xde, 0xad, 0xbe, 0xef, 0x00, 0xff];
    let hex = bytes_to_hex(&bytes);
    assert_eq!(hex, "deadbeef00ff");
    let decoded = hex_to_bytes(&hex).unwrap();
    assert_eq!(decoded, bytes);
}

#[test]
fn test_hex_with_prefix() {
    let decoded = hex_to_bytes("0xabcd").unwrap();
    assert_eq!(decoded, vec![0xab, 0xcd]);
}

#[test]
fn test_encode_decode_transfer() {
    let codec = make_token_abi();
    let data = json!({
        "from": "alice",
        "to": "bob",
        "quantity": "1.0000 WAX",
        "memo": "hello"
    });

    let encoded = codec.encode_action("transfer", &data).unwrap();
    let decoded = codec.decode_action("transfer", &encoded).unwrap();

    assert_eq!(decoded["from"], "alice");
    assert_eq!(decoded["to"], "bob");
    assert_eq!(decoded["quantity"], "1.0000 WAX");
    assert_eq!(decoded["memo"], "hello");
}

#[test]
fn test_encode_decode_create() {
    let codec = make_token_abi();
    let data = json!({
        "issuer": "eosio.token",
        "maximum_supply": "1000000000.0000 WAX"
    });

    let encoded = codec.encode_action("create", &data).unwrap();
    let decoded = codec.decode_action("create", &encoded).unwrap();

    assert_eq!(decoded["issuer"], "eosio.token");
    assert_eq!(decoded["maximum_supply"], "1000000000.0000 WAX");
}

#[test]
fn test_encode_decode_table_row() {
    let codec = make_token_abi();
    let data = json!({
        "supply": "500.0000 WAX",
        "max_supply": "1000000000.0000 WAX",
        "issuer": "test.pasha"
    });

    let encoded = codec.encode_table_row("stat", &data).unwrap();
    let decoded = codec.decode_table_row("stat", &encoded).unwrap();

    assert_eq!(decoded["supply"], "500.0000 WAX");
    assert_eq!(decoded["max_supply"], "1000000000.0000 WAX");
    assert_eq!(decoded["issuer"], "test.pasha");
}

#[test]
fn test_primitives() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_prim",
            "base": "",
            "fields": [
                {"name": "b", "type": "bool"},
                {"name": "u8", "type": "uint8"},
                {"name": "u16", "type": "uint16"},
                {"name": "u32", "type": "uint32"},
                {"name": "u64", "type": "uint64"},
                {"name": "i8", "type": "int8"},
                {"name": "i32", "type": "int32"},
                {"name": "f32", "type": "float32"},
                {"name": "f64", "type": "float64"}
            ]
        }],
        "actions": [{"name": "testprim", "type": "test_prim", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({
        "b": true,
        "u8": 255,
        "u16": 65535,
        "u32": 4294967295u64,
        "u64": "18446744073709551615",
        "i8": -128,
        "i32": -1,
        "f32": 3.14,
        "f64": 2.718281828
    });

    let encoded = codec.encode_action("testprim", &data).unwrap();
    let decoded = codec.decode_action("testprim", &encoded).unwrap();

    assert_eq!(decoded["b"], true);
    assert_eq!(decoded["u8"], 255);
    assert_eq!(decoded["u16"], 65535);
    assert_eq!(decoded["u32"], 4294967295u64);
    assert_eq!(decoded["u64"], "18446744073709551615");
    assert_eq!(decoded["i8"], -128);
    assert_eq!(decoded["i32"], -1);
}

#[test]
fn test_array_type() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_arr",
            "base": "",
            "fields": [
                {"name": "names", "type": "name[]"},
                {"name": "values", "type": "uint32[]"}
            ]
        }],
        "actions": [{"name": "testarr", "type": "test_arr", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({
        "names": ["alice", "bob", "charlie"],
        "values": [1, 2, 3, 100]
    });

    let encoded = codec.encode_action("testarr", &data).unwrap();
    let decoded = codec.decode_action("testarr", &encoded).unwrap();

    let names = decoded["names"].as_array().unwrap();
    assert_eq!(names.len(), 3);
    assert_eq!(names[0], "alice");
    assert_eq!(names[1], "bob");
    assert_eq!(names[2], "charlie");

    let values = decoded["values"].as_array().unwrap();
    assert_eq!(values.len(), 4);
    assert_eq!(values[3], 100);
}

#[test]
fn test_optional_type() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_opt",
            "base": "",
            "fields": [
                {"name": "label", "type": "string"},
                {"name": "memo", "type": "string?"}
            ]
        }],
        "actions": [{"name": "testopt", "type": "test_opt", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();

    // With value present
    let data = json!({"label": "hi", "memo": "world"});
    let encoded = codec.encode_action("testopt", &data).unwrap();
    let decoded = codec.decode_action("testopt", &encoded).unwrap();
    assert_eq!(decoded["label"], "hi");
    assert_eq!(decoded["memo"], "world");

    // With null
    let data = json!({"label": "hi", "memo": null});
    let encoded = codec.encode_action("testopt", &data).unwrap();
    let decoded = codec.decode_action("testopt", &encoded).unwrap();
    assert_eq!(decoded["label"], "hi");
    assert!(decoded["memo"].is_null());
}

#[test]
fn test_variant_type() {
    let abi_json = r#"{
        "structs": [
            {
                "name": "test_var",
                "base": "",
                "fields": [
                    {"name": "value", "type": "my_variant"}
                ]
            }
        ],
        "actions": [{"name": "testvar", "type": "test_var", "ricardian_contract": ""}],
        "tables": [],
        "types": [],
        "variants": [
            {"name": "my_variant", "types": ["uint32", "string", "name"]}
        ]
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();

    // Variant as uint32
    let data = json!({"value": ["uint32", 42]});
    let encoded = codec.encode_action("testvar", &data).unwrap();
    let decoded = codec.decode_action("testvar", &encoded).unwrap();
    let v = decoded["value"].as_array().unwrap();
    assert_eq!(v[0], "uint32");
    assert_eq!(v[1], 42);

    // Variant as string
    let data = json!({"value": ["string", "hello"]});
    let encoded = codec.encode_action("testvar", &data).unwrap();
    let decoded = codec.decode_action("testvar", &encoded).unwrap();
    let v = decoded["value"].as_array().unwrap();
    assert_eq!(v[0], "string");
    assert_eq!(v[1], "hello");
}

#[test]
fn test_type_alias() {
    let abi_json = r#"{
        "types": [
            {"new_type_name": "account_name", "type": "name"}
        ],
        "structs": [{
            "name": "test_alias",
            "base": "",
            "fields": [
                {"name": "owner", "type": "account_name"}
            ]
        }],
        "actions": [{"name": "testalias", "type": "test_alias", "ricardian_contract": ""}],
        "tables": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"owner": "alice"});
    let encoded = codec.encode_action("testalias", &data).unwrap();
    let decoded = codec.decode_action("testalias", &encoded).unwrap();
    assert_eq!(decoded["owner"], "alice");
}

#[test]
fn test_struct_inheritance() {
    let abi_json = r#"{
        "structs": [
            {
                "name": "base_struct",
                "base": "",
                "fields": [
                    {"name": "id", "type": "uint64"}
                ]
            },
            {
                "name": "derived_struct",
                "base": "base_struct",
                "fields": [
                    {"name": "name", "type": "string"}
                ]
            }
        ],
        "actions": [{"name": "derived", "type": "derived_struct", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"id": "42", "name": "test"});
    let encoded = codec.encode_action("derived", &data).unwrap();
    let decoded = codec.decode_action("derived", &encoded).unwrap();
    assert_eq!(decoded["id"], "42");
    assert_eq!(decoded["name"], "test");
}

#[test]
fn test_checksum256() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_hash",
            "base": "",
            "fields": [
                {"name": "hash", "type": "checksum256"}
            ]
        }],
        "actions": [{"name": "testhash", "type": "test_hash", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let hash = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    let data = json!({"hash": hash});
    let encoded = codec.encode_action("testhash", &data).unwrap();
    let decoded = codec.decode_action("testhash", &encoded).unwrap();
    assert_eq!(decoded["hash"], hash);
}

#[test]
fn test_extended_asset() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_ext",
            "base": "",
            "fields": [
                {"name": "value", "type": "extended_asset"}
            ]
        }],
        "actions": [{"name": "testext", "type": "test_ext", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({
        "value": {
            "quantity": "100.0000 WAX",
            "contract": "eosio.token"
        }
    });
    let encoded = codec.encode_action("testext", &data).unwrap();
    let decoded = codec.decode_action("testext", &encoded).unwrap();
    assert_eq!(decoded["value"]["quantity"], "100.0000 WAX");
    assert_eq!(decoded["value"]["contract"], "eosio.token");
}

#[test]
fn test_symbol_field() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_sym",
            "base": "",
            "fields": [
                {"name": "sym", "type": "symbol"},
                {"name": "code", "type": "symbol_code"}
            ]
        }],
        "actions": [{"name": "testsym", "type": "test_sym", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"sym": "4,WAX", "code": "WAX"});
    let encoded = codec.encode_action("testsym", &data).unwrap();
    let decoded = codec.decode_action("testsym", &encoded).unwrap();
    assert_eq!(decoded["sym"], "4,WAX");
    assert_eq!(decoded["code"], "WAX");
}

#[test]
fn test_unknown_action_error() {
    let codec = make_token_abi();
    let result = codec.encode_action("nonexistent", &json!({}));
    assert!(result.is_err());
}

#[test]
fn test_missing_field_error() {
    let codec = make_token_abi();
    let data = json!({"from": "alice"});
    let result = codec.encode_action("transfer", &data);
    assert!(result.is_err());
}

#[test]
fn test_bytes_type() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_bytes",
            "base": "",
            "fields": [
                {"name": "data", "type": "bytes"}
            ]
        }],
        "actions": [{"name": "testbytes", "type": "test_bytes", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"data": "deadbeef"});
    let encoded = codec.encode_action("testbytes", &data).unwrap();
    let decoded = codec.decode_action("testbytes", &encoded).unwrap();
    assert_eq!(decoded["data"], "deadbeef");
}

#[test]
fn test_nested_struct() {
    let abi_json = r#"{
        "structs": [
            {
                "name": "inner",
                "base": "",
                "fields": [
                    {"name": "x", "type": "uint32"},
                    {"name": "y", "type": "uint32"}
                ]
            },
            {
                "name": "outer",
                "base": "",
                "fields": [
                    {"name": "label", "type": "string"},
                    {"name": "point", "type": "inner"}
                ]
            }
        ],
        "actions": [{"name": "testnest", "type": "outer", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({
        "label": "origin",
        "point": {"x": 10, "y": 20}
    });
    let encoded = codec.encode_action("testnest", &data).unwrap();
    let decoded = codec.decode_action("testnest", &encoded).unwrap();
    assert_eq!(decoded["label"], "origin");
    assert_eq!(decoded["point"]["x"], 10);
    assert_eq!(decoded["point"]["y"], 20);
}

#[test]
fn test_time_point() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_time",
            "base": "",
            "fields": [
                {"name": "tp", "type": "time_point"},
                {"name": "tps", "type": "time_point_sec"}
            ]
        }],
        "actions": [{"name": "testtime", "type": "test_time", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"tp": "1609459200000000", "tps": 1609459200});
    let encoded = codec.encode_action("testtime", &data).unwrap();
    let decoded = codec.decode_action("testtime", &encoded).unwrap();
    assert_eq!(decoded["tp"], "1609459200000000");
    assert_eq!(decoded["tps"], 1609459200);
}

#[test]
fn test_varint32_zigzag() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_var32",
            "base": "",
            "fields": [
                {"name": "vu", "type": "varuint32"},
                {"name": "vi", "type": "varint32"}
            ]
        }],
        "actions": [{"name": "testvr", "type": "test_var32", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({"vu": 300, "vi": -150});
    let encoded = codec.encode_action("testvr", &data).unwrap();
    let decoded = codec.decode_action("testvr", &encoded).unwrap();
    assert_eq!(decoded["vu"], 300);
    assert_eq!(decoded["vi"], -150);
}

#[test]
fn test_int128_as_string() {
    let abi_json = r#"{
        "structs": [{
            "name": "test_128",
            "base": "",
            "fields": [
                {"name": "big_u", "type": "uint128"},
                {"name": "big_i", "type": "int128"}
            ]
        }],
        "actions": [{"name": "test128", "type": "test_128", "ricardian_contract": ""}],
        "tables": [], "types": [], "variants": []
    }"#;

    let codec = AbiCodec::from_json(abi_json).unwrap();
    let data = json!({
        "big_u": "340282366920938463463374607431768211455",
        "big_i": "-170141183460469231731687303715884105728"
    });
    let encoded = codec.encode_action("test128", &data).unwrap();
    let decoded = codec.decode_action("test128", &encoded).unwrap();
    assert_eq!(decoded["big_u"], "340282366920938463463374607431768211455");
    assert_eq!(decoded["big_i"], "-170141183460469231731687303715884105728");
}
