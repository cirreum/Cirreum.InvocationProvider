namespace Cirreum.Invocation.Connections;

/// <summary>
/// Server-initiated push over the active connection. Fire-and-forget; no expected
/// response. Connection-bound; available only for invocations from long-lived sources.
/// </summary>
/// <remarks>
/// <para>
/// Multi-producer safe. Within a single producer, sends preserve issue order. Across
/// producers, ordering is unspecified — apps that need cross-producer order serialize
/// their producers themselves (e.g., shared queue + single drain task).
/// </para>
/// <para>
/// The source adapter handles underlying socket synchronization. Apps do not need to lock.
/// </para>
/// </remarks>
public interface IConnectionSender {

	/// <summary>Send a payload over the current connection.</summary>
	ValueTask SendAsync<T>(T payload, CancellationToken cancellationToken = default);

	/// <summary>
	/// Send a payload addressed to a specific method/event name. Used by adapters that
	/// route by method-keyed receive handlers (SignalR <c>HubConnection.On&lt;T&gt;</c>,
	/// WebSocket message-type discriminators).
	/// </summary>
	ValueTask SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);

}
