# Cirreum.InvocationProvider

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.InvocationProvider/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.InvocationProvider.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.InvocationProvider/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.InvocationProvider/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.InvocationProvider?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.InvocationProvider/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

Core abstractions library for the Cirreum Invocation framework.

---

## Mental model

`Cirreum.InvocationProvider` is the **server-side foundational peer** to `Cirreum.Core`:

- `Cirreum.Core` — cross-host foundation (identity, operation, conductor — works in Server, Wasm, Serverless).
- `Cirreum.InvocationProvider` — server-side foundation (inbound invocation seam — only meaningful where the framework receives invocations).

Both are L2 packages. Both are always-present in their applicable hosts. Neither references the other. Together they form the L2 Core layer that the spine and provider tracks build upon.

You can think of `Cirreum.InvocationProvider` as `Cirreum.Core.Server` in spirit (though we keep its actual name aligned with the Provider-track convention because it also serves the second role below).

## Two roles in one package

1. **Foundational L2 abstractions for the inbound seam** — `IInvocationContext`, `IInvocationContextAccessor`, `InvocationContextAccessor` (default AsyncLocal impl), `InvocationSources` in the root `Cirreum.Invocation` namespace. The persistent-connection sub-types (`IConnection`, `IConnectionLifecycle`, `IConnectionOutbound`) live in the `Cirreum.Invocation.Connections` sub-namespace because they apply only to long-lived invocation sources. These are referenced by `Cirreum.Services.Server` and the spine — non-pluggable, always-on framework foundation.

2. **Provider-track abstractions for pluggable invocation sources** — `InvocationProviderRegistrar` abstract base + settings hierarchy. L3 per-source packages (`Cirreum.Invocation.SignalR`, `.WebSockets`, future `.gRPC`) derive from these to plug new invocation sources in alongside the framework default (HTTP).

## What's in the box

| Type | Role |
|---|---|
| `IInvocationContext` | Per-invocation ambient context |
| `IInvocationContextAccessor` / `InvocationContextAccessor` | AsyncLocal-backed singleton accessor (matches `IHttpContextAccessor` convention) |
| `InvocationSources` | Const class with framework-known source values |
| `InvocationProviderRegistrar<,>` | Abstract base for L3 per-source registrars (two-phase: `Register` + `Map`) |
| `InvocationProviderSettings<>` / `InvocationProviderInstanceSettings` | Settings hierarchy bound from `Cirreum:Invocation:Providers:{Source}:Instances:{key}` |
| `IConnection (Cirreum.Invocation.Connections)` | Per-connection state for long-lived sources |
| `IConnectionLifecycle (Cirreum.Invocation.Connections)` | App-side `OnConnectedAsync` / `OnDisconnectedAsync` hook |
| `IConnectionOutbound (Cirreum.Invocation.Connections)` | Server-initiated push primitive |

## What lives elsewhere

| Type | Lives in |
|---|---|
| `IInvocationBuilder` + impl + `AddInvocation()` extension | `Cirreum.Runtime.InvocationProvider` (L4) |
| Typed `Items`-slot extensions (`Get/SetAuthenticatedScheme`, etc.) | `Cirreum.Runtime.InvocationProvider` (L4) |
| Upgrade-time `Items`-slot copy helper | `Cirreum.Runtime.InvocationProvider` (L4) |
| HTTP→`IInvocationContext` middleware + `UserAccessor` refactor | `Cirreum.Services.Server` (L3) |
| Per-source concrete registrars (SignalR, WebSocket, …) | `Cirreum.Invocation.{Source}` (L3, Phase 5) |
| App-facing `Add{Source}<T>(key)` extensions | `Cirreum.Runtime.Invocation.{Source}` (L5, Phase 5) |

## Who consumes it

- **`Cirreum.Services.Server`** (L3) — `UserAccessor` injects `IInvocationContextAccessor`; HTTP middleware constructs `IInvocationContext` per request.
- **`Cirreum.Runtime.InvocationProvider`** (L4) — implements `IInvocationBuilder`; consumes `InvocationProviderRegistrar` machinery to compose L3 per-source impls.
- **`Cirreum.Invocation.{Source}`** (L3, Phase 5) — per-source concrete registrars derive from `InvocationProviderRegistrar`.

App code does not normally reference this package directly — it shows up transitively through the runtime.

## Contribution Guidelines

1. **Be conservative with new abstractions** — the API surface must remain stable and meaningful.
2. **Limit dependency expansion** — only add foundational, version-stable dependencies.
3. **Favor additive, non-breaking changes** — breaking changes ripple through the entire ecosystem.
4. **Include thorough unit tests** — all primitives and patterns should be independently testable.
5. **Document architectural decisions** — context and reasoning should be clear for future maintainers.
6. **Follow .NET conventions** — use established patterns from `Microsoft.Extensions.*` libraries.

## Versioning

Follows [Semantic Versioning](https://semver.org/). Given its foundational role, major bumps are rare and carefully considered.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
