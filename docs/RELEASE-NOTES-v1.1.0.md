# Cirreum.InvocationProvider 1.1.0 — `IConnectionLifecycle` learns the disconnect-reason

Adds a typed `DisconnectInfo` parameter to `IConnectionLifecycle.OnDisconnectedAsync`. Source adapters now surface the *why* of every disconnect — graceful close vs. abrupt termination, the underlying exception (if any), and a human-readable reason — to app-side lifecycle hooks through a single uniform seam.

Strictly additive in spirit; signature-breaking in mechanics. Enabled by the no-published-implementer window before the first L3 source adapter (`Cirreum.Invocation.SignalR 1.0.0`) ships.

---

## Why this release exists

When `Cirreum.InvocationProvider 1.0.1` shipped, `IConnectionLifecycle.OnDisconnectedAsync` looked like:

```csharp
ValueTask OnDisconnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);
```

That signature gives apps "the connection just disconnected" but nothing else. Every long-lived transport has richer disconnect information available:

| Transport | What the transport offers |
|---|---|
| SignalR | `Exception?` — non-null on abort/error, null on graceful close |
| Raw WebSocket | `WebSocketCloseStatus` + `WebSocketCloseStatusDescription`, plus optional abort exception |
| gRPC streaming | Status code + status detail, plus optional abort exception |

Dropping all of it at the L2 abstraction boundary forces apps that need disconnect-reason (audit, alerting, retry policies, distinguishing client-initiated logout from network failure) to bypass the seam and reach for transport-native APIs. That defeats the unification purpose of `IConnectionLifecycle`.

The fix is a typed disconnect-info record that every adapter populates from its native disconnect signal.

---

## What's new

### `DisconnectInfo` record

```csharp
public sealed record DisconnectInfo(
    bool WasGraceful,
    Exception? Exception = null,
    string? Reason = null);
```

| Field | Semantic |
|---|---|
| `WasGraceful` | `true` for transport-defined clean closes (peer-initiated normal close, no abort, no exception). `false` for any abrupt termination. |
| `Exception` | The exception surfaced by the transport at disconnect, when one was reported. `null` for graceful disconnects and adapters that don't expose exceptions on this path. |
| `Reason` | Free-form human-readable description — close-status description for WebSocket, exception message for SignalR, status detail for gRPC. **For diagnostics, not control flow** — consumers should branch on `WasGraceful` / `Exception`, not parse `Reason`. |

Per-adapter mappings:

- **SignalR**: `WasGraceful = exception is null; Exception = exception; Reason = exception?.Message`
- **Raw WebSocket**: `WasGraceful = closeStatus == WebSocketCloseStatus.NormalClosure; Reason = closeStatusDescription; Exception = abortException` if any
- **gRPC streaming**: `WasGraceful = status.StatusCode == OK; Reason = status.Detail; Exception = abortException` if any

Future adapters populate from their respective close-status APIs.

### Updated `IConnectionLifecycle.OnDisconnectedAsync` signature

```diff
- ValueTask OnDisconnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);
+ ValueTask OnDisconnectedAsync(
+     IInvocationConnection connection,
+     DisconnectInfo info,
+     CancellationToken cancellationToken);
```

Apps now write:

```csharp
public ValueTask OnDisconnectedAsync(
    IInvocationConnection connection,
    DisconnectInfo info,
    CancellationToken cancellationToken) {

    if (info.WasGraceful) {
        _logger.LogInformation("Connection {Id} closed cleanly", connection.ConnectionId);
    } else if (info.Exception is not null) {
        _logger.LogWarning(info.Exception,
            "Connection {Id} aborted: {Reason}", connection.ConnectionId, info.Reason);
    }

    return ValueTask.CompletedTask;
}
```

---

## Why this is 1.1.0 and not 2.0.0

Strictly per SemVer, adding a positional parameter to a public interface method is a breaking change to every implementer. We're calling this 1.1.0 (Minor) rather than 2.0.0 (Major) because:

- **Zero published consumers exist** for v1.0.1's `IConnectionLifecycle`. The interface is six days old; the first source adapter (`Cirreum.Invocation.SignalR 1.0.0`) hadn't shipped at the time of this change. No external code in the wild implements this interface.
- **Cirreum app-side framework code does not implement it either.** `Cirreum.Services.Server`, `Cirreum.Runtime.Server`, and `Cirreum.Runtime.AuthorizationProvider` all consume `IInvocationContext` but none implements `IConnectionLifecycle` (HTTP doesn't have a connection lifecycle).
- **The cost of being slightly looser on SemVer here is bounded** (the breaking change can only affect a non-existent population), and the cost of bumping to 2.0.0 (`MIGRATION-v2.md` ceremony, suggesting to readers that something major changed) is real.

Same window-of-no-consumers reasoning that motivated `1.0.1`'s `IConnectionOutbound → IConnectionSender` rename — captured here for the same kind of post-release-pre-adoption correction.

If any consumer surfaces between 1.0.1 and 1.1.0 indexing, the migration is a single-token interface signature update (see Migration in `CHANGELOG.md`).

---

## What this enables

The first L3 source adapter, `Cirreum.Invocation.SignalR 1.0.0`, ships against this enriched contract from line one — its HubFilter constructs `new DisconnectInfo(exception is null, exception, exception?.Message)` from SignalR's `OnDisconnectedAsync(HubLifetimeContext, Exception?)` parameter and passes it through to lifecycle hooks. Apps gain typed disconnect-reason awareness on day one of SignalR support.

---

## Compatibility

- **Source-incompatible** with any existing `IConnectionLifecycle` implementation (zero known to exist).
- **Binary-incompatible** with any compiled assembly implementing `IConnectionLifecycle` (zero known to exist).
- All other public surface (`IInvocationContext`, `IInvocationContextAccessor`, `IInvocationConnection`, `IConnectionSender`, the registrar bases, etc.) is unchanged.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.1.0`.
- [`Cirreum.Invocation.SignalR 1.0.0`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) (forthcoming) — first L3 source adapter, ships against this enriched contract.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision.
