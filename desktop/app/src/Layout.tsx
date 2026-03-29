import { Outlet, NavLink, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Send,
  QrCode,
  Settings,
  Wallet,
  Import,
  LogOut,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import { lockWallet } from "@/api/client";

const navItems = [
  { to: "/", icon: LayoutDashboard, label: "Dashboard" },
  { to: "/send", icon: Send, label: "Send" },
  { to: "/receive", icon: QrCode, label: "Receive" },
  { to: "/import", icon: Import, label: "Import" },
  { to: "/settings", icon: Settings, label: "Settings" },
];

export default function Layout() {
  const navigate = useNavigate();

  async function handleLock() {
    try {
      await lockWallet();
    } catch { /* ignore */ }
    navigate("/unlock");
  }

  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar */}
      <aside className="flex w-56 flex-col border-r bg-card">
        <div className="flex h-14 items-center gap-2 px-4">
          <Wallet className="size-5 text-primary" />
          <span className="text-base font-semibold">Neo Wallet</span>
        </div>
        <Separator />
        <nav className="flex-1 space-y-1 p-2">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                  isActive
                    ? "bg-primary/10 text-primary"
                    : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                )
              }
            >
              <item.icon className="size-4" />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <Separator />
        <div className="p-2">
          <Button
            variant="ghost"
            className="w-full justify-start gap-3 text-muted-foreground"
            onClick={handleLock}
          >
            <LogOut className="size-4" />
            Lock Wallet
          </Button>
        </div>
        <div className="border-t p-3">
          <p className="text-xs text-muted-foreground">v0.1.0</p>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-y-auto">
        <div className="mx-auto max-w-5xl p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
