namespace Cirreum.Invocation.Connections;

using System.Security.Claims;

/// <summary>
/// Per-connection state for a long-lived inbound connection that hosts many invocations.
/// Lifetime: from connection upgrade through disconnect. Set on
/// <see cref="IInvocationContext.Connection"/> for invocations from long-lived sources
/// (SignalR, raw WebSocket, gRPC streaming); <see langword="null"/> for stateless sources
/// (HTTP, gRPC unary, queue handlers).
/// </summary>
/// <remarks>
/// Describes the actual transport-layer connection — the persistent socket through which
/// many invocations arrive. The connection itself is not an invocation; it's the carrier
/// across which invocations are delivered. Each invocation on the connection gets its own
/// <see cref="IInvocationContext"/> with this <see cref="IInvocationConnection"/> attached.
/// </remarks>
public interface IInvocationConnection {

	/// <summary>Unique identifier assigned by the source adapter at upgrade.</summary>
	/// <remarks>
	/// Adapter-assigned, unique per running server, stable for connection lifetime.
	/// Used as the key in the per-server connection registry.
	/// </remarks>
	string ConnectionId { get; }

	/// <summary>
	/// The authenticated principal resolved at upgrade. Immutable post-upgrade.
	/// </summary>
	ClaimsPrincipal User { get; }

	/// <summary>
	/// UTC timestamp of upgrade.
	/// </summary>
	DateTimeOffset ConnectedAtUtc { get; }

	/// <summary>
	/// Per-connection bag for ambient state with whole-connection lifetime
	/// (cached <c>IUserState</c>, ApplicationUserCache that should outlive a single
	/// invocation, application-defined connection-scoped state).
	/// </summary>
	/// <remarks>
	/// <strong>Distinct from</strong> <see cref="IInvocationContext.Items"/> — which is
	/// per-invocation. Slots placed here outlive individual invocations on the same
	/// connection.
	/// </remarks>
	IDictionary<object, object?> Items { get; }

	/// <summary>
	/// Identifier of the source that produced this connection (e.g.
	/// <see cref="InvocationSources.SignalR"/>). Same value as
	/// <see cref="IInvocationContext.InvocationSource"/> for invocations on this
	/// connection.
	/// </summary>
	string InvocationSource { get; }

	/// <summary>
	/// Cancellation tied to the connection's lifetime; fires on disconnect. Linked
	/// into each invocation's <see cref="IInvocationContext.Aborted"/> so disposing
	/// the connection cancels in-flight work.
	/// </summary>
	CancellationToken Aborted { get; }

	/// <summary>
	/// Aborts the connection. Cancels <see cref="Aborted"/>, signaling all in-flight
	/// reads, receive loops, and pending invocations on this connection to terminate.
	/// The source adapter then runs its disconnect path (closing the underlying transport
	/// and dispatching <see cref="IConnectionLifecycle.OnDisconnectedAsync"/> hooks).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Idempotent — calling on an already-aborted connection is a no-op. Returns
	/// immediately without waiting for the disconnect path to complete; the actual
	/// teardown is observable via <see cref="IConnectionLifecycle.OnDisconnectedAsync"/>.
	/// </para>
	/// <para>
	/// Use cases: server-side timeout, app-level kick (auth lapsed mid-connection),
	/// or a handler that orchestrates multiple sockets and needs to terminate the
	/// inbound transport when an outbound dependency drops.
	/// </para>
	/// </remarks>
	void Abort();

	/// <summary>
	/// Server-initiated push of a typed payload over this connection. Fire-and-forget;
	/// no expected response.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Multi-producer safe. Within a single producer, sends preserve issue order. Across
	/// producers, ordering is unspecified — apps that need cross-producer order serialize
	/// their producers themselves (e.g., shared queue + single drain task).
	/// </para>
	/// <para>
	/// Serialization is transport-specific. For SignalR the payload flows through the
	/// configured <c>IHubProtocol</c> (JSON or MessagePack). For raw WebSocket the
	/// payload is JSON-serialized using the active handler's <c>SerializerOptions</c>
	/// (apps override that property to plug in a source-generated
	/// <c>JsonTypeInfoResolver</c> for AOT/trim-friendly apps) and sent as a Text frame.
	/// </para>
	/// <para>
	/// For WebSocket apps that need to bypass JSON serialization and write raw frames
	/// (binary protocols, audio, pre-serialized payloads), downcast to
	/// <c>IWebSocketConnection</c> and call <c>SendBytesAsync</c>.
	/// </para>
	/// </remarks>
	ValueTask SendAsync<T>(T payload, CancellationToken cancellationToken = default);

	/// <summary>
	/// Server-initiated push of a typed payload addressed to a specific method/event
	/// name. Used by transports that route by method-keyed receive handlers (SignalR
	/// <c>HubConnection.On&lt;T&gt;</c>; WebSocket apps that implement their own
	/// method-dispatch protocol on top of the wire format).
	/// </summary>
	/// <remarks>
	/// Same fire-and-forget, multi-producer-safe semantics as
	/// <see cref="SendAsync{T}(T, CancellationToken)"/>. For SignalR, <paramref name="method"/>
	/// is the receive-handler name registered by the client (<c>connection.on(method, ...)</c>).
	/// For WebSocket, the framework wraps the payload in a
	/// <c>{ "method": "...", "payload": ... }</c> JSON envelope sent as a Text frame.
	/// </remarks>
	ValueTask SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);

}
