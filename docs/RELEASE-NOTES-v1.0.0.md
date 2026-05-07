# Cirreum.InvocationProvider 1.0.0 — Unified inbound invocation seam

The L2 Core abstractions for the **Cirreum Invocation** framework. Models inbound dispatch as a family of pluggable *invocation sources* (SignalR, raw WebSockets, gRPC, queue triggers) that deliver work into the framework's pipeline, behind a single ambient `IInvocationContext` seam every source populates uniformly.

This is the foundational L2 peer to `Cirreum.Core` — the "server-side foundation" alongside Core's "cross-host foundation." Both are always-present in their applicable hosts; neither references the other; together they form the L2 Core layer that the spine and provider tracks build upon.

Anchored by [ADR-0002 — Unified `IInvocationContext` Seam](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md).

---

## Why this release exists

A typical Cirreum server fans in from multiple inbound surfaces in the same host:

- HTTP REST endpoints — request/response, the framework default.
- SignalR Hub methods — long-lived, bidirectional, per-method invocations on a persistent connection.
- Raw WebSocket frames — long-lived, custom protocols (e.g. voice/IVA bridges).
- Queue / timer triggers (serverless) — fire-and-process invocations from infrastructure.
- gRPC unary and streaming calls (future).

Pre-`1.0.0`, the framework had no architectural axis to express this variation. The layer model named one architectural axis — *host environment* (Server / Wasm / Serverless) — but assumed a single inbound source (HTTP request/response) implicitly. Long-lived sources were treated as exceptions: planned as a layered capability on top of `Cirreum.Runtime.Server`, with a composite `IUserStateAccessor` and a registration "dance" to bridge between an HTTP-bound `UserAccessor` and a connection-bound resolver.

The fix is to name the second axis explicitly: each inbound source is a peer in an *Invocation* family, all populating the same `IInvocationContext` seam. `UserAccessor` reads from one ambient accessor regardless of which source is active. No composites. No registration gymnastics. Long-lived shapes get a sub-namespace for their additional state (`Cirreum.Invocation.Connections`); stateless shapes use the base namespace alone.

This package is the L2 abstractions root. It plays two roles in the framework:

1. **Foundational L2 abstractions for the inbound seam** — `IInvocationContext` and friends, referenced by the spine. Always-on; not pluggable.
2. **Provider-track abstractions** — `InvocationProviderRegistrar` abstract base and settings hierarchy, derived from by L3 per-source packages (`Cirreum.Invocation.SignalR`, etc.) that plug new sources in alongside the framework default.

---

## What's in the box

### Root namespace `Cirreum.Invocation`

```csharp
public interface IInvocationContext {
    ClaimsPrincipal User { get; }
    IDictionary<object, object?> Items { get; }
    IServiceProvider Services { get; }
    CancellationToken Aborted { get; }
    string InvocationSource { get; }
    IInvocationConnection? Connection { get; }
}

public interface IInvocationContextAccessor {
    IInvocationContext? Current { get; }
    void Set(IInvocationContext invocation);
    void Clear();
}

public sealed class InvocationContextAccessor : IInvocationContextAccessor { /* AsyncLocal-backed */ }

public static class InvocationSources {
    public const string Http       = "http";
    public const string SignalR    = "signalr";
    public const string WebSocket  = "websocket";
    public const string GrpcUnary  = "grpc-unary";
    public const string GrpcStream = "grpc-stream";
    public const string Queue      = "queue";
    public const string Timer      = "timer";
}

public abstract class InvocationProviderRegistrar<TSettings, TInstanceSettings>
    : IProviderRegistrar<TSettings, TInstanceSettings>
        where TInstanceSettings : InvocationProviderInstanceSettings
        where TSettings : InvocationProviderSettings<TInstanceSettings> {
    public ProviderType ProviderType => ProviderType.Invocation;
    public abstract string ProviderName { get; }
    public virtual void Register(TSettings, IServiceCollection, IConfiguration) { ... }
    public virtual void Map(TSettings, IEndpointRouteBuilder) { ... }
    protected abstract void RegisterSource(...);
    protected abstract void MapSource(...);
}
```

### Sub-namespace `Cirreum.Invocation.Connections`

Persistent-connection sub-types. Apply only to long-lived invocation sources (SignalR, raw WebSocket, gRPC streaming). Apps using only stateless sources (HTTP, queue triggers) never need to import this namespace.

```csharp
public interface IInvocationConnection {
    string ConnectionId { get; }
    ClaimsPrincipal User { get; }
    DateTimeOffset ConnectedAtUtc { get; }
    IDictionary<object, object?> Items { get; }
    string InvocationSource { get; }
    CancellationToken Aborted { get; }
}

public interface IConnectionLifecycle {
    ValueTask<bool> OnConnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);
    ValueTask OnDisconnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);
}

public interface IConnectionOutbound {
    ValueTask SendAsync<T>(T payload, CancellationToken cancellationToken = default);
    ValueTask SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);
}
```

### `Cirreum.Invocation.Configuration`

Settings hierarchy bound from `Cirreum:Invocation:Providers:{ProviderName}:Instances:{key}`:

```csharp
public abstract class InvocationProviderSettings<TInstanceSettings>
    : IProviderSettings<TInstanceSettings>
        where TInstanceSettings : InvocationProviderInstanceSettings {
    public Dictionary<string, TInstanceSettings> Instances { get; set; } = [];
}

public abstract class InvocationProviderInstanceSettings : IProviderInstanceSettings {
    public bool Enabled { get; set; }
    public string Path { get; set; } = "";
    public string? Scheme { get; set; }                    // ref to a Cirreum:Authorization scheme
    public string? Name { get; set; }                      // optional ConnectionStrings lookup
    public IConfigurationSection? Section { get; set; }    // raw section for impl-specific sub-binding
}
```

---

## Architectural position

```
L2 Core (foundational; peers; no cross-refs)
  Cirreum.Core                    cross-host (works in Server/Wasm/Serverless)
  Cirreum.InvocationProvider      THIS PACKAGE — server-side; "Cirreum.Core.Server"
  Cirreum.AuthorizationProvider   pluggable provider track abstractions
  Cirreum.IdentityProvider        pluggable provider track abstractions

L3 Infrastructure
  Cirreum.Services.Server         consumes IInvocationContextAccessor; UserAccessor reads it
  Cirreum.Invocation.{Source}     per-source concrete registrars (Phase 5)

L4 Runtime
  Cirreum.Runtime.InvocationProvider   IInvocationBuilder + AddInvocation() + helpers + typed extensions
  Cirreum.Runtime.Server               composes spine; does NOT reference Runtime.InvocationProvider

L5 Runtime Extensions
  Cirreum.Runtime.Invocation.{Source}  per-source runtime extensions (Phase 5)
```

This package doesn't reference `Cirreum.Core` directly (L2 peers don't cross-reference). Types that need to bridge concepts from both — typed `Items`-slot extensions, the upgrade-time slot-copy helper, the app-facing `IInvocationBuilder` — live one layer up in `Cirreum.Runtime.InvocationProvider` (L4), where both L2 packages can be referenced.

---

## Supersedes `Cirreum.Connections` v1.0.0

The shipped `Cirreum.Connections` v1.0.0 was the first attempt at this seam, framed as a layered capability on top of `Cirreum.Runtime.Server`. ADR-0001's implementation phase surfaced architectural drift (the "fourth runtime" misframing, layer-reference bumps, composite `IUserStateAccessor` registration dance) that traced back to the unnamed dispatch-shape axis. ADR-0002 names the axis as *invocation source*, replaces the composite with one ambient seam, and renames the family to *Invocation*.

Type rename map:

| `Cirreum.Connections` v1.0.0 | `Cirreum.InvocationProvider` v1.0.0 |
|---|---|
| `IRealtimeConnection` | `IInvocationConnection` (in `Cirreum.Invocation.Connections`) |
| `IRealtimeInvocation` | `IInvocationContext` (unified per-message seam) |
| `IRealtimeConnectionAccessor` | `IInvocationContextAccessor` (single ambient seam) |
| `IRealtimeInvocationAccessor` | (removed — single accessor) |
| `RealtimeConnectionAccessor` | `InvocationContextAccessor` |
| `RealtimeInvocationAccessor` | (removed) |
| `IConnectionAuthLifecycle` | `IConnectionLifecycle` (in `Cirreum.Invocation.Connections`) |
| `IRealtimeOutbound` | `IConnectionOutbound` (in `Cirreum.Invocation.Connections`); method-keyed overload added |
| `ConnectionContextKeys` | (removed — slot keys flow through `Cirreum.Security.AuthenticationContextKeys` in `Cirreum.Core`) |

`Cirreum.Connections` v1.0.0 is deprecated on NuGet. No external consumers exist; the cost is internal restructure.

---

## Architectural principles

> **The act of being invoked is a first-class architectural axis, peer to host environment.**

Pre-`1.0.0`, the framework's layer model named only one axis (host environment: Server / Wasm / Serverless) and assumed HTTP as the invocation source implicitly. This release names the second axis (invocation source) explicitly. Each transport — HTTP, SignalR, raw WebSocket, gRPC, queue trigger — is a peer member of the Invocation family. HTTP is the framework default for ergonomics, but architecturally it's a peer like the others.

> **One ambient seam per concern; no composites.**

`UserAccessor` reads from one accessor — `IInvocationContextAccessor` — regardless of which source produced the invocation. Every source adapter populates the seam at its inbound boundary. There is no composite, no registration "dance," no per-source identity-resolution branching.

> **Persistent-connection state lives in a sub-namespace because it applies only to long-lived sources.**

`IInvocationConnection`, `IConnectionLifecycle`, and `IConnectionOutbound` describe persistent connections that host many invocations. Apps using only stateless sources never need to know they exist. Splitting into `Cirreum.Invocation.Connections` makes the boundary explicit; the type names are role-descriptive (Connection-prefixed) rather than use-case-flavored (Realtime-/Streaming-prefixed) — the same connection primitives serve realtime, streaming, notifications, voice, and any other use case built on persistent inbound connections.

> **Foundational L2 packages don't cross-reference.**

`Cirreum.InvocationProvider` and `Cirreum.Core` are peers. Neither references the other. Spine (`Services.*` / `Runtime.*`) and provider-track packages reference both freely, but the foundations themselves stay independent. Types that genuinely need both layers — typed `Items`-slot extensions, the upgrade-time slot-copy helper, the app-facing builder — live at L4 where both L2 references are valid.

---

## Coordinated downstream releases

This is the foundation; downstream packages compose on top:

- **`Cirreum.Providers`** — bumped to 1.1.1 with `ProviderType.Invocation = 8` (renamed from the never-consumed `ProviderType.Connection` of 1.1.0).
- **`Cirreum.InvocationProvider.Client`** v1.0.0 — client-side abstractions for inbound-push subscription on the WASM/M2M side. Asymmetric to this package on purpose; client doesn't have a fan-in problem.
- **`Cirreum.Runtime.InvocationProvider`** v1.0.0 (next) — L4 runtime: `IInvocationBuilder`, `AddInvocation()`, `MapInvocationEndpoints()`, `RegisterInvocationProvider<,,>()`, the typed `Items`-slot extensions, and the upgrade-time slot-copy helper.
- **`Cirreum.Services.Server`** (next minor) — `UserAccessor` refactor to read `IInvocationContextAccessor`; HTTP→`IInvocationContext` middleware as the framework's default invocation source.
- **`Cirreum.Runtime.Server`** (next minor) — `Build()` composition wires the HTTP middleware. Does not reference `Cirreum.Runtime.InvocationProvider` (L4 peers don't intra-layer reference); apps that need long-lived sources pick up the L4 runtime transitively through the L5 source packages they install.

L3 per-source registrars (`Cirreum.Invocation.SignalR`, `.WebSockets`) and L5 runtime extensions (`Cirreum.Runtime.Invocation.SignalR`, `.WebSockets`) follow in Phase 5 alongside their first end-to-end validation against a live SignalR client and the closed-source raw-WebSocket reference codebase.

---

## Compatibility

- **No external consumers** for `Cirreum.Connections` v1.0.0 → no compatibility shims. The rename is a clean source break with the package deprecated and unlisted on NuGet.
- **`InvocationContextAccessor` is registered as singleton** (matches `IHttpContextAccessor` convention). Per-flow isolation comes from `AsyncLocal<T>`, not from DI scope. The accessor instance has no per-request mutable state.
- **Two-namespace organization** keeps simple consumers from importing types they don't need. HTTP-only apps see `Cirreum.Invocation`. Long-lived-source consumers add `using Cirreum.Invocation.Connections;` for `IInvocationConnection` / `IConnectionLifecycle` / `IConnectionOutbound`.
- **`Items`-bag access pattern**: L3 framework code (e.g. `UserAccessor`, the role claims transformer) uses raw dictionary access against `AuthenticationContextKeys.*`. L4+ consumers and app-side code use the typed extensions shipped from `Cirreum.Runtime.InvocationProvider`. The split is intentional: the L3 sites are foundational and centralized; the typed extensions exist to prevent SCATTERED dictionary access across many higher-layer call sites.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.0.0`.
- The downstream package READMEs and changelogs for the coordinated updates that ship alongside this release.
