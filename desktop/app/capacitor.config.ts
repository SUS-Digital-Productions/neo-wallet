import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.sus.neowallet",
  appName: "Neo Wallet",
  webDir: "dist",
  server: {
    // In production the app runs fully offline from bundled assets.
    // During development you can point to the Vite dev server:
    // url: "http://localhost:1420",
    // cleartext: true,
  },
  android: {
    minWebViewVersion: "60.0.0",
  },
  ios: {
    scheme: "NeoWallet",
  },
};

export default config;
