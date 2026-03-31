use tauri::Manager;
use tauri::Emitter;

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

/// Emit any `esr://` URIs to the React frontend.
fn emit_deep_links(app: &tauri::AppHandle, urls: Vec<url::Url>) {
    for u in urls {
        let raw = u.as_str().to_string();
        if raw.starts_with("esr:") {
            println!("[tauri] deep-link ESR URI: {raw}");
            let _ = app.emit("esr-deep-link", raw);
        }
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let mut builder = tauri::Builder::default();

    // On desktop, use single-instance plugin so subsequent deep-link clicks
    // reuse the already-running window instead of spawning a new process.
    #[cfg(desktop)]
    {
        builder = builder.plugin(tauri_plugin_single_instance::init(|_app, _argv, _cwd| {
            // Deep-link args are forwarded automatically when the "deep-link"
            // feature is enabled on the single-instance plugin.
        }));
    }

    builder
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_deep_link::init())
        .setup(|app| {
            // Register esr:// scheme at runtime for dev/non-installed builds
            #[cfg(any(windows, target_os = "linux"))]
            {
                use tauri_plugin_deep_link::DeepLinkExt;
                let _ = app.deep_link().register_all();
            }

            // Check if app was launched via a deep link
            #[cfg(desktop)]
            {
                use tauri_plugin_deep_link::DeepLinkExt;
                if let Ok(Some(urls)) = app.deep_link().get_current() {
                    emit_deep_links(app.handle(), urls);
                }
            }

            // Listen for deep-link events while running
            let handle = app.handle().clone();
            app.listen("deep-link://new-url", move |event| {
                if let Ok(urls) = serde_json::from_str::<Vec<url::Url>>(event.payload()) {
                    emit_deep_links(&handle, urls);
                }
            });

            // Start .NET backend sidecar
            let handle2 = app.handle().clone();
            match start_backend(&handle2) {
                Ok(_child) => {
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
