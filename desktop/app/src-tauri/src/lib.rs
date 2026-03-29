use tauri::Manager;

/// Start the .NET sidecar backend and return the process handle.
fn start_backend(app: &tauri::AppHandle) -> Result<tauri_plugin_shell::process::CommandChild, String> {
    let shell = app.shell();
    let (mut rx, child) = shell
        .sidecar("NeoWallet.Backend")
        .map_err(|e| format!("failed to create sidecar command: {e}"))?
        .spawn()
        .map_err(|e| format!("failed to spawn sidecar: {e}"))?;

    // Log sidecar stdout/stderr in background
    tauri::async_runtime::spawn(async move {
        while let Some(event) = rx.recv().await {
            match event {
                tauri_plugin_shell::process::CommandEvent::Stdout(line) => {
                    println!("[backend:stdout] {}", String::from_utf8_lossy(&line));
                }
                tauri_plugin_shell::process::CommandEvent::Stderr(line) => {
                    eprintln!("[backend:stderr] {}", String::from_utf8_lossy(&line));
                }
                _ => {}
            }
        }
    });

    Ok(child)
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let handle = app.handle().clone();
            match start_backend(&handle) {
                Ok(_child) => {
                    // Keep child alive for the duration of the app.
                    // In production you may store it in app state to kill on exit.
                    println!("[tauri] .NET backend sidecar started");
                }
                Err(e) => {
                    eprintln!("[tauri] Failed to start backend: {e}");
                }
            }
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
