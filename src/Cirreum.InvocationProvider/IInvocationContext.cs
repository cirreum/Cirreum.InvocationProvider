namespace Cirreum.Invocation;

using System.Security.Claims;
using Cirreum.Invocation.Connections;

/// <summary>
/// Per-message ambient context for an inbound invocation into the framework. Populated
/// by the source adapter at the inbound seam (HTTP middleware, SignalR HubFilter,
/// WebSocket frame handler, etc.) and read through the pipeline by
/// <c>IUserStateAccessor</c>, the conductor, validators, authorizers, audit, and any
/// other ambient consumer.
/// </summary>
/// <remarks>
/// <para>
/// One instance per inbound invocation; lifetime equals the invocation. Resolved
/// ambiently via <see cref="IInvocationContextAccessor.Current"/>.
/// </para>
/// <para>
/// Slots into the existing context chain at the transport seam:
/// <c>IInvocationContext → IUserState → OperationContext → AuthorizationContext</c>.
/// Each stage is established by a different pipeline component.
/// </para>
/// </remarks>
public interface IInvocationContext {

	/// <summary>The authenticated principal for this invocation.</summary>
	/// <remarks>
	/// Immutable for the invocation's lifetime. For invocations from long-lived sources,
	/// mirrors <see cref="IInvocationConnection.User"/> (set once at upgrade). For stateless sources,
	/// derived from the inbound request's authenticated principal.
	/// </remarks>
	ClaimsPrincipal User { get; }

	/// <summary>
	/// Per-invocation bag for ambient state with single-invocation lifetime (cached
	/// <c>IUserState</c>, scheme stamp, application user cache, correlation values,
	/// and any framework slot whose lifetime is one invocation).
	/// </summary>
	/// <remarks>
	/// Distinct dictionary from <see cref="Connection"/>'s <see cref="IInvocationConnection.Items"/>.
	/// Cleared (or simply discarded with the invocation) at invocation end. Mutation-safe
	/// within a single invocation; not safe to share across invocations.
	/// </remarks>
	IDictionary<object, object?> Items { get; }

	/// <summary>
	/// The DI scope established by the source adapter for this invocation. Resolves
	/// scoped services correctly for one inbound invocation. Never <see langword="null"/>.
	/// </summary>
	IServiceProvider Services { get; }

	/// <summary>Cancellation tied to the invocation's lifetime.</summary>
	/// <remarks>
	/// For invocations from long-lived sources, also fires when
	/// <see cref="IInvocationConnection.Aborted"/> fires.
	/// </remarks>
	CancellationToken Aborted { get; }

	/// <summary>
	/// Diagnostic identifier for the source that produced this invocation (see
	/// <see cref="InvocationSources"/> for the framework-known values). For telemetry
	/// and routing, not security.
	/// </summary>
	string InvocationSource { get; }

	/// <summary>
	/// The long-lived connection this invocation belongs to, or <see langword="null"/>
	/// for stateless invocation sources (HTTP, gRPC unary, queue handlers).
	/// </summary>
	IInvocationConnection? Connection { get; }

}
