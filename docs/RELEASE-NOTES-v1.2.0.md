# Cirreum.InvocationProvider 1.2.0 — connections gain explicit termination

Adds `Abort()` to `IInvocationConnection`. Long-lived connections (SignalR, WebSocket, future gRPC streaming) now expose a uniform termination primitive that cancels in-flight reads, drains the connection, and triggers the source adapter's disconnect path. Closes a real gap that surfaced while validating the WebSocket invocation provider against the IVA Twilio media-stream pattern: handlers that orchestrate multiple sockets need a way to terminate the inbound transport when an outbound dependency drops.

Strictly additive in spirit; signature-breaking in mechanics for any external implementer of `IInvocationConnection` (none exist — this interface is implemented only by transport adapters we own).

---

## Why this release exists

When `Cirreum.InvocationProvider 1.1.0` shipped, `IInvocationConnection` exposed read-only state — `ConnectionId`, `User`, `Items`, `Aborted` — but no way to *initiate* termination. The cancellation token was readable but not writable; only the source adapter could cancel it (e.g. when the underlying transport closed).

This works fine for the simple case where the framework owns the read loop and the connection is single-purpose. It breaks down when a handler orchestrates multiple sockets:

```
Framework (blocked):  await twilioSocket.ReceiveAsync(...)   ← waiting for next Twilio frame
Handler  (blocked):   await aiSocket.ReceiveAsync(...)       ← waiting for next AI frame
```

If the AI socket closes first, the handler knows it's time to end the call — but the framework is still blocked reading from Twilio. Without `Abort()`, the handler has no clean way to break out, and the connection sits there until Twilio's keepalive eventually times out the socket.

The IVA reference codebase solves this by owning both read loops and using `Task.WhenAny` + a shared `CancellationTokenSource`. Cirreum's framework owns the inbound loop, so the handler needs a way to ask the framework to stop. `Abort()` is that way.

---

## What's new

### `IInvocationConnection.Abort()`

```csharp
public interface IInvocationConnection {
    // ... existing members ...

    /// <summary>
    /// Aborts the connection. Cancels Aborted, signaling all in-flight reads,
    /// receive loops, and pending invocations to terminate. The source adapter
    /// then runs its disconnect path.
    /// </summary>
    void Abort();
}
```

| Behavior | Detail |
|---|---|
| Idempotent | Calling on an already-aborted connection is a no-op. |
| Non-blocking | Returns immediately; teardown is observable via `IConnectionLifecycle.OnDisconnectedAsync`. |
| Universal | Implemented by every long-lived source adapter (SignalR, WebSocket, future gRPC streaming). |

Per-adapter mappings:

- **SignalR**: wraps `HubCallerContext.Abort()` — SignalR's native termination path. Cancels `ConnectionAborted`, drains the connection, triggers Hub `OnDisconnectedAsync`.
- **Raw WebSocket**: cancels the linked `CancellationTokenSource` backing `Aborted`. The frame loop's `ReceiveAsync(connection.Aborted)` throws `OperationCanceledException`, the loop exits cleanly, and `OnDisconnectedAsync` runs as if the close was orderly.
- **gRPC streaming** (forthcoming): will cancel the streaming call and surface a status to the peer.

### Use cases

```csharp
// 1. Server-side timeout
if (idleFor > TimeSpan.FromMinutes(30)) {
    connection.Abort();
}

// 2. Auth lapsed mid-connection
if (!await reauth.IsStillValidAsync(connection.User)) {
    connection.Abort();
}

// 3. Handler orchestrating multiple sockets (the IVA case)
public override async Task OnConnectedAsync(CancellationToken ct) {
    _aiSocket = await ConnectToAiAsync(ct);

    _aiTask = Task.Run(async () => {
        try {
            await ReadFromAiAsync(_aiSocket, ct);
        } finally {
            // AI ended — terminate the inbound transport too
            Connection!.Abort();
        }
    }, ct);
}
```

---

## Why this is 1.2.0 and not 2.0.0

Strictly per SemVer, adding a required member to a public interface is a breaking change to every implementer. We're calling this 1.2.0 (Minor) rather than 2.0.0 (Major) because:

- **Zero external implementers exist.** `IInvocationConnection` is implemented only by transport adapters — `SignalRConnection` (in `Cirreum.Invocation.SignalR`) and `WebSocketConnection` (in `Cirreum.Invocation.WebSockets`). Both are framework-owned and ship coordinated updates alongside this release. Apps consume `IInvocationConnection` but don't implement it.
- **Same window-of-no-consumers reasoning** that motivated `1.1.0`'s `IConnectionLifecycle.OnDisconnectedAsync` signature change — captured here for the same kind of pre-adoption framework correction.
- **The cost of being slightly looser on SemVer here is bounded** (the breaking change affects a closed, framework-owned implementer set), and the cost of bumping to 2.0.0 (`MIGRATION-v2.md` ceremony, suggesting to readers that something major changed) overstates the impact.

If any external implementer surfaces, the migration is one method addition — see the per-adapter examples above.

---

## Coordinated downstream work

This release ships in lockstep with:

- **`Cirreum.Invocation.SignalR 1.0.3`** — adds `SignalRConnection.Abort()` wrapping `HubCallerContext.Abort()`. Bumps `Cirreum.InvocationProvider` dependency to 1.2.0.
- **`Cirreum.Invocation.WebSockets 1.0.0`** (first release) — ships against this contract from line one; `WebSocketConnection.Abort()` cancels the linked CTS backing `Aborted`.

The `Cirreum.Runtime.InvocationProvider` and `Cirreum.Runtime.Invocation.*` packages don't reference `Abort()` directly — they consume `IInvocationConnection` through the seam, which is unchanged for consumers.

---

## Compatibility

- **Source-incompatible** with any existing external `IInvocationConnection` implementation (zero known to exist).
- **Binary-incompatible** with any compiled assembly implementing `IInvocationConnection` externally (zero known to exist).
- **Source- and binary-compatible** for *consumers* of `IInvocationConnection` (apps, framework runtime). The interface gains a member; consumer call sites are unchanged.
- All other public surface (`IInvocationContext`, `IInvocationContextAccessor`, `IConnectionLifecycle`, `IConnectionSender`, the registrar bases, etc.) is unchanged.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.2.0`.
- `RELEASE-NOTES-v1.1.0.md` — companion `IConnectionLifecycle.OnDisconnectedAsync` signature change, same window-of-no-consumers rationale.
- [`Cirreum.Invocation.WebSockets 1.0.0`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) (forthcoming) — first user of `Abort()`, motivated this addition.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision.
