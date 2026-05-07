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
	/// disposed. Exceptions are absorbed by the framework.
	/// </summary>
	ValueTask OnDisconnectedAsync(IInvocationConnection connection, CancellationToken cancellationToken);

}
