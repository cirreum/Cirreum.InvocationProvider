namespace Cirreum.Invocation.Connections;

/// <summary>
/// App-side hook invoked by the source adapter at connection lifecycle boundaries
/// (long-lived sources only). Apps register an implementation via DI; the adapter
/// resolves and invokes it (if any) at upgrade and disconnect.
/// </summary>
/// <remarks>
/// Long-lived invocation sources only (SignalR, raw WebSocket, gRPC streaming). Stateless
/// sources (HTTP, gRPC unary, queue handlers) do not invoke this hook — they have no
/// connection lifecycle.
/// </remarks>
public interface IConnectionLifecycle {

	/// <summary>
	/// Called after upgrade completes and identity context has been copied to the
	/// connection. Return <see langword="false"/> or throw to reject the connection
	/// (the adapter aborts the upgrade; client sees normal upgrade-rejection).
	/// </summary>
	/// <remarks>
	/// Runs inside a synthetic invocation scope established by the adapter so that
	/// <c>IUserStateAccessor</c> and other ambient consumers work normally inside the
	/// hook. See ADR-0002 transport-adapter invariant #7.
	/// </remarks>
	ValueTask<bool> OnConnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);

	/// <summary>
	/// Called after the adapter detects a disconnect, before connection resources are
	/// disposed. Receives a <see cref="DisconnectInfo"/> describing whether the close
	/// was graceful, any reported exception, and a human-readable reason. Exceptions
	/// thrown from this hook are absorbed by the framework.
	/// </summary>
	/// <param name="connection">The connection that disconnected.</param>
	/// <param name="info">
	/// Adapter-populated disconnect circumstances. See <see cref="DisconnectInfo"/> for
	/// per-transport mappings (SignalR exception, WebSocket close status, gRPC status).
	/// </param>
	/// <param name="cancellationToken">
	/// Connection-tied cancellation; already canceled by the time this hook runs (the
	/// disconnect is what fired it). Provided for API symmetry with
	/// <see cref="OnConnectedAsync"/> and to flow into any cancellable cleanup the hook
	/// chooses to perform.
	/// </param>
	ValueTask OnDisconnectedAsync(
		IInvocationConnection connection,
		DisconnectInfo info,
		CancellationToken cancellationToken);

}
