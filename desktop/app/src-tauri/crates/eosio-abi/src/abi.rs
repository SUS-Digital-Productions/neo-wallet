use serde::{Deserialize, Serialize};

/// Full EOSIO ABI definition, parsed from the standard ABI JSON format.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct AbiDef {
    #[serde(default)]
    pub version: String,
    #[serde(default)]
    pub types: Vec<AbiTypeDef>,
    #[serde(default)]
    pub structs: Vec<AbiStruct>,
    #[serde(default)]
    pub actions: Vec<AbiAction>,
    #[serde(default)]
    pub tables: Vec<AbiTable>,
    #[serde(default)]
    pub variants: Vec<AbiVariantDef>,
    #[serde(default)]
    pub action_results: Vec<AbiActionResult>,
}

/// A type alias (e.g., `account_name` → `name`).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiTypeDef {
    pub new_type_name: String,
    #[serde(rename = "type")]
    pub type_: String,
}

/// A struct definition with optional base (inheritance).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiStruct {
    pub name: String,
    #[serde(default)]
    pub base: String,
    #[serde(default)]
    pub fields: Vec<AbiField>,
}

/// A single field within an ABI struct.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiField {
    pub name: String,
    #[serde(rename = "type")]
    pub type_: String,
}

/// An action declaration mapping action name → struct type.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiAction {
    pub name: String,
    #[serde(rename = "type")]
    pub type_: String,
    #[serde(default)]
    pub ricardian_contract: String,
}

/// A table declaration mapping table name → row struct type.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiTable {
    pub name: String,
    #[serde(default)]
    pub index_type: String,
    #[serde(rename = "type")]
    pub type_: String,
    #[serde(default)]
    pub key_names: Vec<String>,
    #[serde(default)]
    pub key_types: Vec<String>,
}

/// A variant definition (tagged union).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiVariantDef {
    pub name: String,
    pub types: Vec<String>,
}

/// An action result declaration.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AbiActionResult {
    pub name: String,
    pub result_type: String,
}
