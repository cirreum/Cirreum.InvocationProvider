# Changelog

All notable changes to **Cirreum.InvocationProvider** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-05-07

### Changed

- **Renamed `IConnection` → `IInvocationConnection`** (in `Cirreum.Invocation.Connections`). The original name was too generic and conflicted conceptually with `Microsoft.AspNetCore.Connections.ConnectionContext`, SignalR's `HubConnectionContext`, and other framework "connection" types. The new name explicitly signals "this is the Invocation framework's view of a long-lived connection," eliminating the confusion when read in isolation.
- **Renamed `IConnectionOutbound` → `IConnectionSender`** (in `Cirreum.Invocation.Connections`). "Outbound" described direction but not action; "Sender" mirrors the `SendAsync` methods the interface exposes and reads as a concrete connection facet alongside `IConnectionLifecycle`. `IConnectionLifecycle` keeps its name.

### Migration from 1.0.0

Two single-token find/replace edits:

- `IConnection` → `IInvocationConnection` (use whole-word matching to avoid touching the longer `IConnectionLifecycle` / `IConnectionSender` names)
- `IConnectionOutbound` → `IConnectionSender`

The `Cirreum.Invocation.Connections` namespace import is unchanged. No external consumers are known to exist for v1.0.0 yet — these renames were applied immediately after release while the package was still NuGet-indexing.

## [1.0.0] - 2026-05-07

### Added

Initial release of the Cirreum Invocation Provider abstractions library — the L2 Core "server-side foundation" peer to `Cirreum.Core` (which holds the cross-host identity/operation chain). Together they form the L2 Core layer that the spine and provider tracks build upon. Anchored by [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md).

The framework models **how things are invoked into the framework**. Each registered transport — SignalR, WebSocket, gRPC, queue trigger — is an *invocation source* that delivers or manifests work into the framework's pipeline. HTTP is the framework's default invocation source.

**Unified inbound seam (interfaces + default ambient impl):**

- `IInvocationContext` — per-invocation ambient context: `User`, `Items`, `Services`, `Aborted`, `InvocationSource`, optional `Connection`. The single ambient seam every invocation source populates uniformly.
- `IInvocationContextAccessor` — ambient accessor with `Current` getter and explicit `Set`/`Clear` methods.
- `InvocationContextAccessor` — default `AsyncLocal<T>`-backed implementation. Registered as singleton (matches `IHttpContextAccessor`'s convention — async-local provides per-flow isolation, no per-scope state on the accessor instance).
- `InvocationSources` — const class with framework-known source values (`Http`, `SignalR`, `WebSocket`, `GrpcUnary`, `GrpcStream`, `Queue`, `Timer`).

**Long-lived connection sub-namespace (`Cirreum.Invocation.Connections`):**

The types below describe persistent connections that host many invocations. They live in a dedicated sub-namespace because they apply only to long-lived invocation sources (SignalR, raw WebSocket, gRPC streaming) — apps using only HTTP never need to import them. Use cases on top of these primitives include realtime/streaming/notifications/voice — but the primitives themselves describe the connection role, not any specific use case.

- `IConnection` — per-connection state for long-lived inbound connections that host many invocations: `ConnectionId`, `User`, `ConnectedAtUtc`, `Items`, `InvocationSource`, `Aborted`. Set on `IInvocationContext.Connection` for invocations from connection-oriented sources; `null` for stateless sources. *(Renamed to `IInvocationConnection` in 1.0.1 — see Unreleased entry.)*
- `IConnectionLifecycle` — App-side `OnConnectedAsync` / `OnDisconnectedAsync` hook.
- `IConnectionOutbound` — server-initiated push primitive with overloads for raw payload sends and method-keyed sends.

**Provider-pattern abstractions (extensibility points for L3 per-source packages):**

- `InvocationProviderRegistrar<TSettings, TInstanceSettings>` — abstract base for Invocation-family registrars. Two-phase: `Register` (services) + `Map` (endpoints). Hardcodes `ProviderType.Invocation`. Handles instance iteration, dedup tracking, validation, and section binding. Concrete L3 per-source registrars (e.g., `Cirreum.Invocation.SignalR`) derive from this and implement `RegisterSource` / `MapSource`.
- `InvocationProviderSettings<TInstanceSettings>` — settings container with `Instances` dictionary.
- `InvocationProviderInstanceSettings` — base instance settings: `Enabled`, `Path`, `Scheme`, `Name`, `Section`.

### Out of scope (lives in higher layers)

The following types are **not** in this package — they live in their natural homes:

- `IInvocationBuilder` + default impl + `AddInvocation()` extension + helpers + typed `Items` extensions → `Cirreum.Runtime.InvocationProvider` (L4 Runtime, where `Cirreum.Core` and ASP.NET hosting types can both be referenced).
- HTTP→`IInvocationContext` middleware + `UserAccessor` refactor → `Cirreum.Services.Server` (L3 Infrastructure).
- Per-source concrete registrars (SignalR, WebSocket) → `Cirreum.Invocation.{Source}` (L3 Infrastructure, added in Phase 5).
- App-facing `Add{Source}<T>(key)` extensions → `Cirreum.Runtime.Invocation.{Source}` (L5 Runtime Extensions, added in Phase 5).

### Supersedes

Replaces the deprecated `Cirreum.Connections` v1.0.0. Type rename map:

| `Cirreum.Connections` v1.0.0 | New home |
|---|---|
| `IRealtimeConnection` | `IConnection` (this package — renamed to `IInvocationConnection` in 1.0.1) |
| `IRealtimeInvocation` | `IInvocationContext` (this package — unified per-invocation seam) |
| `IRealtimeConnectionAccessor` | `IInvocationContextAccessor` (this package — single ambient seam) |
| `IRealtimeInvocationAccessor` | (removed — single accessor) |
| `RealtimeConnectionAccessor` | `InvocationContextAccessor` (this package) |
| `RealtimeInvocationAccessor` | (removed) |
| `IConnectionAuthLifecycle` | `IConnectionLifecycle` (this package) |
| `IRealtimeOutbound` | renamed to `IConnectionSender` (this package, `Cirreum.Invocation.Connections` namespace — was `IConnectionOutbound` in 1.0.0; method-keyed overload added) |
| `ConnectionContextKeys` | (removed — slot keys flow through `Cirreum.Security.AuthenticationContextKeys` in `Cirreum.Core`) |
