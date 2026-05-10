# Cirreum.InvocationProvider 1.3.0 — `IConnectionSender` collapses into `IInvocationConnection.SendAsync`

Lifts the two `SendAsync<T>` overloads onto `IInvocationConnection` directly and removes the standalone `IConnectionSender` interface. The split between connection-state-accessor and connection-send-service was modeled after the Authorization track's `IAuthorizationContextAccessor` + `IAuthorizationService` pair, but the analogy didn't hold: auth services have real behavior (rule evaluation, policy resolution); the connection sender was a thin DI-resolved wrapper that did one thing — read `IInvocationContextAccessor.Current.Connection` and forward to it.

Strictly additive in spirit; signature-breaking in mechanics for any external implementer of `IInvocationConnection` (none exist — this interface is implemented only by transport adapters we own).

---

## Why this release exists

`IConnectionSender` was always a thin layer with no behavior of its own. The mental test that finally settled it:

> "If I need `IConnectionSender`, do I also need to know whether I'm in an active invocation? Yes (or it'll throw). And if I have to check that, I'm reading `IInvocationContextAccessor.Current` anyway. At which point I have `.Connection`. At which point `IConnectionSender` adds nothing."

Every native long-lived transport API puts Send on the connection-shaped object directly:

- **SignalR**: `HubCallerContext` → `Clients.Caller.SendAsync(...)`
- **ASP.NET WebSocket**: `WebSocket.SendAsync(...)`
- **gRPC streaming**: `IServerStreamWriter<T>.WriteAsync(...)`

Splitting state and send-behavior across two interfaces is convenient but unnatural. The connection IS the thing you send through. This release reshapes the framework to match that.

---

## What's new

### `IInvocationConnection.SendAsync<T>` — two overloads

```csharp
public interface IInvocationConnection {
    // ... existing members ...

    ValueTask SendAsync<T>(T payload, CancellationToken cancellationToken = default);
    ValueTask SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);
}
```

| Behavior | Detail |
|---|---|
| Multi-producer safe | Source adapter handles underlying socket synchronization. |
| Order-preserving (within a producer) | Across producers, ordering is unspecified. |
| Fire-and-forget | No expected response. |
| Universal | Implemented by every long-lived source adapter (SignalR, WebSocket, future gRPC streaming). |

Per-adapter mappings:

- **SignalR** — both overloads forward to the captured `ISingleClientProxy.SendAsync(method, payload, ct)`. The SignalR pipeline owns serialization through the configured `IHubProtocol` (JSON or MessagePack — controlled by app via `AddSignalR().AddJsonProtocol(...)` / `.AddMessagePackProtocol()`). The no-method overload uses the runtime payload type name as the SignalR method (matching `connection.on("ChatMessage", ...)`).
- **Raw WebSocket** — JSON-serializes the payload using the active handler's `SerializerOptions` (captured at connection construction), sent as a Text frame. The keyed overload wraps the payload in a `{ method, payload }` envelope. Cross-cutting code reaching the connection through `IInvocationContextAccessor.Current.Connection` automatically uses the same serializer the handler is configured with — including any source-generated `JsonTypeInfoResolver` the app set up for AOT/trim-friendly apps.
- **gRPC streaming** (forthcoming) — typed messages through generated Protobuf stubs, with the same handler-captured-options pattern.

### Use cases

```csharp
// Cross-cutting code (Conductor command handler, validator, etc.) — no
// special service to inject; just reach the ambient connection.
public sealed class GenerateReportHandler(
    IInvocationContextAccessor accessor) : ICommandHandler<GenerateReportCommand> {

    public async ValueTask<Result> Handle(GenerateReportCommand cmd, CancellationToken ct) {
        var connection = accessor.Current?.Connection;
        if (connection is not null) {
            await connection.SendAsync("Progress", new { Percent = 0 }, ct);
            // ... work ...
            await connection.SendAsync("Progress", new { Percent = 100 }, ct);
        }
        return Result.Success(/* ... */);
    }
}
```

---

## What's changed

### `IConnectionSender` consolidated into `IInvocationConnection.SendAsync`

The standalone `IConnectionSender` interface is gone — its two `SendAsync<T>` overloads now live on `IInvocationConnection` itself (see "What's new"). Same operation, more natural shape; cross-cutting code reads `accessor.Current?.Connection` and calls `SendAsync` directly. Captured here as a reshape rather than a removal: the surface didn't disappear, it moved one interface up. Treated as Minor under the same window-of-no-consumers, framework-owned-implementer-set precedent as the 1.1.0 (`OnDisconnectedAsync` signature) and 1.2.0 (`Abort()` interface widening) cascades — `IConnectionSender` is a v1.0.x pre-adoption surface with no external consumers yet.

Migration:

```diff
  public sealed class GenerateReportHandler(
-     IInvocationContextAccessor accessor,
-     IConnectionSender sender) : ICommandHandler<GenerateReportCommand> {
+     IInvocationContextAccessor accessor) : ICommandHandler<GenerateReportCommand> {

      public async ValueTask<Result> Handle(GenerateReportCommand cmd, CancellationToken ct) {
-         var canPush = accessor.Current?.Connection is not null;
-         if (canPush) await sender.SendAsync("Progress", new { Percent = 0 }, ct);
+         var connection = accessor.Current?.Connection;
+         if (connection is not null) await connection.SendAsync("Progress", new { Percent = 0 }, ct);
          // ... work ...
-         if (canPush) await sender.SendAsync("Progress", new { Percent = 100 }, ct);
+         if (connection is not null) await connection.SendAsync("Progress", new { Percent = 100 }, ct);
          return Result.Success(/* ... */);
      }
  }
```

Mechanical, ~3 lines per call site. Apps using `WebSocketHandler.SendAsync(...)` from inside the handler are unaffected — that surface stays.

### Why this is 1.3.0 and not 2.0.0

Same framework-owned-implementer-set reasoning as the `1.1.0` `IConnectionLifecycle.OnDisconnectedAsync` and `1.2.0` `Abort()` releases:

- **Zero external implementers exist** for `IInvocationConnection`. Only transport adapters (`SignalRConnection`, `WebSocketConnection`, future gRPC streaming connection) implement it; all are framework-owned and ship coordinated updates alongside this release.
- **`IConnectionSender` consumers are app-side**, not implementers. The migration is mechanical (one find/replace and ~3 lines per call site).
- **Bumping to 2.0.0 would overstate the impact** — `MIGRATION-v2.md` ceremony, suggesting to readers that something fundamental changed. Nothing fundamental changed; one redundant abstraction was reshaped onto a more natural interface.

If any external implementer of `IInvocationConnection` surfaces, the migration is two method additions — see the per-adapter examples in the L3 release notes.

---

## Coordinated downstream work

This release ships in lockstep with:

- **`Cirreum.Invocation.SignalR 1.2.0`** — implements `SignalRConnection.SendAsync<T>` (forwards to `ISingleClientProxy.SendAsync`); deletes `SignalRConnectionSender`.
- **`Cirreum.Invocation.WebSockets 1.2.0`** — implements `WebSocketConnection.SendAsync<T>` (using captured `JsonSerializerOptions` from the handler); adds public `IWebSocketConnection` interface for raw frame writes; deletes `WebSocketConnectionSender`.
- **`Cirreum.Runtime.Invocation.SignalR 1.1.0`** — flow-through dependency bump; no L5 surface change.
- **`Cirreum.Runtime.Invocation.WebSockets 1.1.0`** — flow-through dependency bump; no L5 surface change.

---

## Compatibility

- **Source-incompatible** with any existing external `IInvocationConnection` implementation (zero known to exist).
- **Binary-incompatible** with any compiled assembly implementing `IInvocationConnection` externally (zero known to exist).
- **Source-incompatible** for app code that injected `IConnectionSender` (typed find/replace migration above; ~3 lines per call site).
- **Source- and binary-compatible** for consumers of `IInvocationConnection` (apps, framework runtime). The interface gains members; consumer call sites are unchanged.
- All other public surface (`IInvocationContext`, `IInvocationContextAccessor`, `IConnectionLifecycle`, `DisconnectInfo`, the registrar bases, etc.) is unchanged.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.3.0`.
- `RELEASE-NOTES-v1.2.0.md` — `IInvocationConnection.Abort()` addition; same window-of-no-consumers rationale.
- `RELEASE-NOTES-v1.1.0.md` — `IConnectionLifecycle.OnDisconnectedAsync` signature change; first instance of the coordinated-cascade pattern.
- [`Cirreum.Invocation.SignalR 1.2.0`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) — SignalR adapter update.
- [`Cirreum.Invocation.WebSockets 1.2.0`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) — WebSocket adapter update + new `IWebSocketConnection` interface.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision.
