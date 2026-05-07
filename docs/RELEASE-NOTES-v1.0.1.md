# Cirreum.InvocationProvider 1.0.1 — Connection-family naming refinements

Two naming refinements applied immediately after the 1.0.0 release:

- `IConnection` → **`IInvocationConnection`** — the original name was too generic and conflicted conceptually (not at compile time, but in code review and documentation) with several other framework "connection" types.
- `IConnectionOutbound` → **`IConnectionSender`** — "Outbound" described *direction* but not *action*. The new name names what the interface does (it sends), mirroring its `SendAsync` methods, and reads cleanly as a connection facet alongside `IConnectionLifecycle`.

Two breaking renames, narrow scope. No behavior changes.

---

## Why this release exists

The shipped 1.0.0 had `IConnection` in the `Cirreum.Invocation.Connections` sub-namespace. Reading code at use sites:

```csharp
public class ChatHub : Hub {
    public Task SendMessage(IConnection conn, ChatMessage msg) { ... }
}
```

A reader's first guess on what `IConnection` is:

- **SignalR's `HubConnectionContext`?** (after all, it's a Hub class)
- **`Microsoft.AspNetCore.Connections.ConnectionContext`?** (Kestrel transport-level)
- **`System.Net.WebSockets.WebSocket`?**
- **Some Cirreum thing?**

Even though there's no compile-time collision (Microsoft uses `ConnectionContext`, not `IConnection`), the **conceptual** ambiguity is real. `IConnection` is the most generic possible name for a connection-related interface — exactly what makes it confusing in a context full of other connection-related types.

The 1.0.1 rename to `IInvocationConnection` makes the type's role unambiguous when read in isolation:

```csharp
public class ChatHub : Hub {
    public Task SendMessage(IInvocationConnection conn, ChatMessage msg) { ... }
}                            //  ↑ unambiguously the Cirreum Invocation framework's connection abstraction
```

The selective rename — only `IConnection` got the `Invocation` prefix; `IConnectionLifecycle` and `IConnectionSender` stayed scoped under the `Connection` family — is intentional. Those two are facets *of* a connection (lifecycle aspect, sender aspect), so the `Connection` prefix correctly groups them. `Connection` alone was the genuinely ambiguous root name.

---

## What's renamed

| Before (1.0.0) | After (1.0.1) | Namespace |
|---|---|---|
| `IConnection` | **`IInvocationConnection`** | `Cirreum.Invocation.Connections` |
| `IConnectionOutbound` | **`IConnectionSender`** | `Cirreum.Invocation.Connections` |
| `IConnectionLifecycle` | unchanged | `Cirreum.Invocation.Connections` |

`IInvocationContext.Connection` property type also updated:

```csharp
public interface IInvocationContext {
    // ...
    IInvocationConnection? Connection { get; }    // ← was IConnection
}
```

Files renamed:
- `Connections/IConnection.cs` → `Connections/IInvocationConnection.cs`
- `Connections/IConnectionOutbound.cs` → `Connections/IConnectionSender.cs`

---

## Migration from 1.0.0

```diff
- using Cirreum.Invocation.Connections;
+ using Cirreum.Invocation.Connections;       // unchanged
- public class ChatHub : Hub {
-     public Task SendMessage(IConnection conn, ChatMessage msg) { ... }
+ public class ChatHub : Hub {
+     public Task SendMessage(IInvocationConnection conn, ChatMessage msg) { ... }
  }
```

Two single-token find/replace edits:

- `IConnection` → `IInvocationConnection` — use **match whole word** to avoid touching the longer `IConnectionLifecycle` / `IConnectionSender` names.
- `IConnectionOutbound` → `IConnectionSender`.

No other types renamed; no other API changes.

---

## Why this is a 1.0.1 (not 2.0.0)

Strict SemVer would call an interface rename a breaking change warranting a major bump. We're doing 1.0.1 because:

- **No external consumers exist for 1.0.0.** The package was indexed on NuGet earlier today (2026-05-07) as part of a coordinated v1.0.0 release across the new Invocation family. No downstream packages have been published against it yet; no app code references it yet.
- **The fix is a naming hygiene improvement caught immediately**, not a behavior change.
- **The actual surface area affected is two symbols** (`IConnection` → `IInvocationConnection`; `IConnectionOutbound` → `IConnectionSender`).
- **A 2.0.0 would imply meaningful evolution between releases**, which isn't what this is — this is the same 1.0.0 release with one symbol-name correction.

The cost of being slightly looser on SemVer here is bounded (no consumers exist); the cost of bumping to 2.0.0 (which would need a `MIGRATION-v2.md` per repo convention, plus suggest to readers that something substantive changed) is real.

If any consumer surfaces between 1.0.0 and 1.0.1 indexing, the migration is trivially mechanical (two find-replace edits).

---

## Architectural principle

> **Type names are honest in isolation.** When a reader sees a type used mid-file (without scrolling to the imports), the name should make the type's role unambiguous in the context of the .NET ecosystem the reader is working in.

`IConnection` failed that test — too generic, too many adjacent meanings in ASP.NET / SignalR / Kestrel. `IInvocationConnection` passes it: the `Invocation` prefix names the framework family unambiguously without requiring the reader to look at imports.

`IConnectionLifecycle` and `IConnectionSender` pass the test too — both name a concrete *facet* of a connection (its lifecycle, its send capability) and follow the convention `I{Root}{Aspect}` for capabilities attached to the connection root.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.0.1`.
- [`docs/RELEASE-NOTES-v1.0.0.md`](RELEASE-NOTES-v1.0.0.md) — the original release this corrects.
