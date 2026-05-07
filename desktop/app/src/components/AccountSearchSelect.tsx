import { useEffect, useMemo, useRef, useState } from "react";
import { Check, ChevronDown, Search, Wallet } from "lucide-react";
import { cn } from "@/lib/utils";
import type { AccountInfo } from "@/api/types";

export function accountSelectValue(account: Pick<AccountInfo, "account" | "authority" | "chainId">) {
  return `${account.account}|${account.authority}|${account.chainId}`;
}

interface AccountSearchSelectProps {
  id?: string;
  accounts: AccountInfo[];
  value: string;
  onValueChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
  emptyText?: string;
}

export function AccountSearchSelect({
  id,
  accounts,
  value,
  onValueChange,
  disabled = false,
  placeholder = "Select account",
  emptyText = "No accounts found",
}: AccountSearchSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const selected = accounts.find((account) => accountSelectValue(account) === value) ?? null;
  const normalizedQuery = query.trim().toLowerCase();
  const filteredAccounts = useMemo(() => {
    if (!normalizedQuery) return accounts;

    return accounts.filter((account) => {
      const haystack = [
        account.account,
        account.authority,
        account.chainName,
        account.chainId,
        account.publicKey,
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(normalizedQuery);
    });
  }, [accounts, normalizedQuery]);

  useEffect(() => {
    if (!open) return;

    function handleDocumentMouseDown(event: MouseEvent) {
      if (!wrapRef.current?.contains(event.target as Node)) setOpen(false);
    }

    document.addEventListener("mousedown", handleDocumentMouseDown);
    inputRef.current?.focus();
    return () => document.removeEventListener("mousedown", handleDocumentMouseDown);
  }, [open]);

  function choose(nextValue: string) {
    onValueChange(nextValue);
    setOpen(false);
    setQuery("");
  }

  return (
    <div ref={wrapRef} className="relative">
      <button
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
        className={cn(
          "flex min-h-11 w-full items-center justify-between gap-3 rounded-md border border-input bg-background px-3 py-2 text-left text-sm shadow-xs outline-none transition-[color,box-shadow]",
          "hover:bg-accent focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
          disabled && "cursor-not-allowed opacity-50",
        )}
      >
        <span className="flex min-w-0 items-center gap-2">
          <Wallet className="size-4 shrink-0 text-muted-foreground" />
          {selected ? (
            <span className="min-w-0">
              <span className="block truncate font-medium">
                {selected.account}@{selected.authority}
              </span>
              <span className="block truncate text-xs text-muted-foreground">
                {selected.chainName}
              </span>
            </span>
          ) : (
            <span className="text-muted-foreground">{placeholder}</span>
          )}
        </span>
        <ChevronDown
          className={cn(
            "size-4 shrink-0 text-muted-foreground transition-transform",
            open && "rotate-180",
          )}
        />
      </button>

      {open && (
        <div className="absolute left-0 right-0 top-full z-50 mt-1 rounded-md border bg-popover p-1 text-popover-foreground shadow-md">
          <div className="relative m-1">
            <Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <input
              ref={inputRef}
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Escape") setOpen(false);
              }}
              placeholder="Search accounts"
              className="h-8 w-full rounded-md border bg-background pl-8 pr-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
            />
          </div>

          <div className="max-h-72 overflow-y-auto p-1">
            {filteredAccounts.length === 0 ? (
              <div className="px-2 py-3 text-sm text-muted-foreground">{emptyText}</div>
            ) : (
              filteredAccounts.map((account) => {
                const optionValue = accountSelectValue(account);
                const selectedOption = optionValue === value;
                return (
                  <button
                    key={optionValue}
                    type="button"
                    onClick={() => choose(optionValue)}
                    className={cn(
                      "flex w-full items-center justify-between gap-3 rounded px-2 py-2 text-left text-sm transition-colors",
                      selectedOption ? "bg-primary/10 text-primary" : "hover:bg-accent",
                    )}
                  >
                    <span className="min-w-0">
                      <span className="block truncate font-medium">
                        {account.account}@{account.authority}
                      </span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {account.chainName} · {account.publicKey.slice(0, 24)}...
                      </span>
                    </span>
                    {selectedOption && <Check className="size-4 shrink-0" />}
                  </button>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
}