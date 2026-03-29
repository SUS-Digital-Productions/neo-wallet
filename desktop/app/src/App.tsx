import { Routes, Route } from "react-router-dom";
import Layout from "./Layout";
import Dashboard from "./pages/Dashboard";
import Send from "./pages/Send";
import Receive from "./pages/Receive";
import Unlock from "./pages/Unlock";
import Settings from "./pages/Settings";
import EsrApproval from "./pages/EsrApproval";
import ImportAccount from "./pages/ImportAccount";

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="send" element={<Send />} />
        <Route path="receive" element={<Receive />} />
        <Route path="import" element={<ImportAccount />} />
        <Route path="settings" element={<Settings />} />
        <Route path="esr" element={<EsrApproval />} />
      </Route>
      <Route path="unlock" element={<Unlock />} />
    </Routes>
  );
}

export default App;
