import { Routes, Route, Navigate, useLocation } from "react-router-dom";
import { useHealth } from "@/api/hooks";
import { Loader2 } from "lucide-react";
import Layout from "./Layout";
import Dashboard from "./pages/Dashboard";
import Send from "./pages/Send";
import Receive from "./pages/Receive";
import Unlock from "./pages/Unlock";
import Settings from "./pages/Settings";
import EsrApproval from "./pages/EsrApproval";
import Keys from "./pages/Keys";
import Networks from "./pages/Networks";
import Accounts from "./pages/Accounts";
import Account from "./pages/Account";
import Utilities from "./pages/Utilities";
import Resources from "./pages/Resources";
import Ram from "./pages/Ram";
import PowerUp from "./pages/PowerUp";
import Vote from "./pages/Vote";
import Permissions from "./pages/Permissions";
import CreateAccount from "./pages/CreateAccount";
import Msig from "./pages/Msig";

function AuthGate({ children }: { children: React.ReactNode }) {
  const { data: health, isLoading, isError } = useHealth({ refetchInterval: 5_000 });
  const location = useLocation();

  // Don't gate the unlock page itself
  if (location.pathname === "/unlock") return <>{children}</>;

  // If the backend is unreachable (mobile / no sidecar), go to unlock
  // so the user isn't stuck on a spinner forever.
  if (isError) {
    return <Navigate to="/unlock" replace />;
  }

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center bg-background">
        <Loader2 className="size-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // Redirect to unlock if wallet isn't unlocked or no token
  if (!health?.walletUnlocked || !sessionStorage.getItem("backend_token")) {
    return <Navigate to="/unlock" replace />;
  }

  return <>{children}</>;
}

function App() {
  return (
    <AuthGate>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="send" element={<Send />} />
          <Route path="receive" element={<Receive />} />
          <Route path="keys" element={<Keys />} />
          <Route path="networks" element={<Networks />} />
          <Route path="accounts" element={<Accounts />} />
          <Route path="account" element={<Account />} />
          <Route path="utilities" element={<Utilities />} />
          <Route path="resources" element={<Resources />} />
          <Route path="ram" element={<Ram />} />
          <Route path="powerup" element={<PowerUp />} />
          <Route path="vote" element={<Vote />} />
          <Route path="permissions" element={<Permissions />} />
          <Route path="create-account" element={<CreateAccount />} />
          <Route path="msig" element={<Msig />} />
          <Route path="settings" element={<Settings />} />
          <Route path="esr" element={<EsrApproval />} />
        </Route>
        <Route path="unlock" element={<Unlock />} />
      </Routes>
    </AuthGate>
  );
}

export default App;
