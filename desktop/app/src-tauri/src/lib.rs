use tauri::Emitter;
use tauri::Listener;
use tauri::Manager;
#[cfg(desktop)]
use tauri::menu::{MenuBuilder, MenuItemBuilder};
#[cfg(desktop)]
use tauri::tray::TrayIconBuilder;
#[cfg(desktop)]
use tauri_plugin_shell::ShellExt;

#[cfg(not(desktop))]
mod mobile_backend;

// ---------------------------------------------------------------------------
// Desktop helpers
// ---------------------------------------------------------------------------

/// Start the .NET sidecar backend and return the process handle (desktop only).
#[cfg(desktop)]
fn start_backend(
    app: &tauri::AppHandle,
) -> Result<tauri_plugin_shell::process::CommandChild, String> {
    let shell = app.shell();
    let (mut rx, child) = shell
        .sidecar("NeoWallet.Backend")
        .map_err(|e| format!("failed to create sidecar command: {e}"))?
        .spawn()
        .map_err(|e| format!("failed to spawn sidecar: {e}"))?;

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

/// Set up desktop-specific features: tray icon, .NET sidecar.
#[cfg(desktop)]
fn setup_desktop(app: &mut tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let open_item = MenuItemBuilder::with_id("open", "Open Neo Wallet").build(app)?;
    let quit_item = MenuItemBuilder::with_id("quit", "Exit").build(app)?;
    let tray_menu = MenuBuilder::new(app)
        .item(&open_item)
        .separator()
        .item(&quit_item)
        .build()?;

    let _tray = TrayIconBuilder::new()
        .menu(&tray_menu)
        .tooltip("Neo Wallet")
        .on_menu_event(move |app, event| match event.id().as_ref() {
            "open" => {
                if let Some(window) = app.get_webview_window("main") {
                    let _ = window.show();
                    let _ = window.set_focus();
                }
            }
            "quit" => {
                println!("[tauri] Exit requested from tray — shutting down.");
                app.exit(0);
            }
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let tauri::tray::TrayIconEvent::DoubleClick { .. } = event {
                let app = tray.app_handle();
                if let Some(window) = app.get_webview_window("main") {
                    let _ = window.show();
                    let _ = window.set_focus();
                }
            }
        })
        .build(app)?;

    println!("[tauri] System tray icon started");

    let handle = app.handle().clone();
    match start_backend(&handle) {
        Ok(_child) => println!("[tauri] .NET backend sidecar started"),
        Err(e) => eprintln!("[tauri] Failed to start backend: {e}"),
    }

    Ok(())
}

// ---------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// JS-callable commands
// ---------------------------------------------------------------------------

/// Bring the main window to the foreground. Called from the renderer when an
/// ESR signing request arrives so the user immediately sees the approval prompt.
#[tauri::command]
fn show_main_window(app: tauri::AppHandle) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.unminimize();
        let _ = window.show();
        let _ = window.set_focus();
        // On Windows / macOS focus can race with the OS — request user attention.
        #[cfg(any(windows, target_os = "macos"))]
        {
            let _ = window.request_user_attention(Some(tauri::UserAttentionType::Informational));
        }
        Ok(())
    } else {
        Err("main window not found".into())
    }
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    #[allow(unused_mut)]
    let mut builder = tauri::Builder::default();

    // Single-instance plugin — desktop only.
    #[cfg(desktop)]
    {
        builder = builder.plugin(tauri_plugin_single_instance::init(|_app, _argv, _cwd| {}));
    }

    builder
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_deep_link::init())
        .invoke_handler(tauri::generate_handler![show_main_window])
        .setup(|app| {
            // Register esr:// scheme at runtime (desktop Linux/Windows)
            #[cfg(any(windows, target_os = "linux"))]
            {
                use tauri_plugin_deep_link::DeepLinkExt;
                let _ = app.deep_link().register_all();
            }

            // Check if app was launched via a deep link (desktop only)
            #[cfg(desktop)]
            {
                use tauri_plugin_deep_link::DeepLinkExt;
                if let Ok(Some(urls)) = app.deep_link().get_current() {
                    emit_deep_links(app.handle(), urls);
                }
            }

            // Listen for deep-link events while running (all platforms)
            let handle = app.handle().clone();
            app.listen("deep-link://new-url", move |event: tauri::Event| {
                if let Ok(urls) = serde_json::from_str::<Vec<url::Url>>(event.payload()) {
                    emit_deep_links(&handle, urls);
                }
            });

            // --- Desktop: tray icon + .NET sidecar ---
            #[cfg(desktop)]
            setup_desktop(app)?;

            // --- Mobile: embedded Rust HTTP backend ---
            #[cfg(not(desktop))]
            {
                let data_dir = app
                    .path()
                    .app_data_dir()
                    .expect("failed to resolve app data directory");
                mobile_backend::start(data_dir);
                println!("[tauri] Mobile embedded backend started");
            }

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
