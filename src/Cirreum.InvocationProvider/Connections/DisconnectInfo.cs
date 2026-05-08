namespace Cirreum.Invocation.Connections;

/// <summary>
/// Describes the circumstances of a connection disconnect, surfaced to
/// <see cref="IConnectionLifecycle.OnDisconnectedAsync"/> by the source adapter.
/// Long-lived sources only.
/// </summary>
/// <remarks>
/// <para>
/// Populated by the source adapter from whatever disconnect-time information the
/// underlying transport offers, normalized onto the three fields here:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>SignalR</term>
///     <description>
///       <c>WasGraceful = exception is null</c>; <c>Exception = exception</c> from the
///       <c>OnDisconnectedAsync(HubLifetimeContext, Exception?)</c> hook;
///       <c>Reason = exception?.Message</c>.
///     </description>
///   </item>
///   <item>
///     <term>Raw WebSocket</term>
///     <description>
///       <c>WasGraceful = closeStatus == WebSocketCloseStatus.NormalClosure</c>;
///       <c>Reason = closeStatusDescription</c>; <c>Exception</c> populated when the
///       socket aborts rather than closes cleanly.
///     </description>
///   </item>
///   <item>
///     <term>gRPC streaming</term>
///     <description>
///       <c>WasGraceful = status.StatusCode == OK</c>; <c>Reason = status.Detail</c>;
///       <c>Exception</c> populated on RPC abort.
///     </description>
///   </item>
/// </list>
/// <para>
/// Consumers should make decisions on the typed fields (<see cref="WasGraceful"/>,
/// <see cref="Exception"/>) rather than parsing <see cref="Reason"/> — Reason is for
/// human-readable diagnostics (logs, audit trails), not control flow.
/// </para>
/// </remarks>
/// <param name="WasGraceful">
/// <see langword="true"/> when the underlying transport reports a clean,
/// protocol-defined close (peer initiated a normal close, no abort, no exception).
/// <see langword="false"/> for any abrupt termination (transport error, abort, host
/// shutdown mid-flight, peer crash, etc.).
/// </param>
/// <param name="Exception">
/// The exception surfaced by the source adapter at disconnect, when one was reported.
/// <see langword="null"/> for graceful disconnects and for adapters that don't surface
/// exceptions on this path.
/// </param>
/// <param name="Reason">
/// Free-form human-readable description of the disconnect — close-status description
/// for WebSocket, exception message for SignalR, status detail for gRPC, etc. For
/// diagnostics; not for control flow.
/// </param>
public sealed record DisconnectInfo(
	bool WasGraceful,
	Exception? Exception = null,
	string? Reason = null);
