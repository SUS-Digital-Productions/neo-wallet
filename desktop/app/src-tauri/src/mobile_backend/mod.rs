// Mobile-only embedded HTTP backend.
//
// On mobile the .NET sidecar cannot run, so this module spins up a small
// axum HTTP server on `127.0.0.1:5199` that implements the same REST API
// the React frontend expects.  The wallet file format (AES-256-CBC with
// PBKDF2 key derivation) is byte-compatible with the .NET desktop backend.

mod crypto;
mod routes;
mod state;

use std::path::PathBuf;

/// Spawn the embedded HTTP server on a background Tokio task.
/// Called from `lib.rs` during Tauri `setup`.
pub fn start(data_dir: PathBuf) {
    std::fs::create_dir_all(&data_dir).expect("failed to create app data directory");
    let wallet_path = data_dir.join("wallet.json");

    let app_state = state::AppState::new(wallet_path);

    tauri::async_runtime::spawn(async move {
        let router = routes::build_router(app_state);

        let listener = tokio::net::TcpListener::bind("127.0.0.1:5199")
            .await
            .expect("failed to bind mobile backend to 127.0.0.1:5199");

        println!("[mobile-backend] Listening on http://127.0.0.1:5199");
        axum::serve(listener, router)
            .await
            .expect("mobile backend server crashed");
    });
}
