namespace Cirreum.Invocation;

/// <summary>
/// Well-known values for <see cref="IInvocationContext.InvocationSource"/>. The property
/// type remains <see cref="string"/> so that L5 source packages can introduce new sources
/// by defining their own constants — these are the framework-known values.
/// </summary>
/// <remarks>
/// Values are stable, lowercase, transport-name based. They are diagnostic and routing
/// metadata, not security boundaries. Adding new well-known sources here is an additive
/// change to the framework surface.
/// </remarks>
public static class InvocationSources {

	/// <summary>HTTP request/response (the framework default invocation source).</summary>
	public const string Http = "http";

	/// <summary>SignalR Hub method invocation.</summary>
	public const string SignalR = "signalr";

	/// <summary>Raw WebSocket frame.</summary>
	public const string WebSocket = "websocket";

	/// <summary>gRPC unary call.</summary>
	public const string GrpcUnary = "grpc-unary";

	/// <summary>gRPC server-streaming call.</summary>
	public const string GrpcStream = "grpc-stream";

	/// <summary>Queue-triggered invocation (Azure Functions queue trigger, AWS SQS handler, etc.).</summary>
	public const string Queue = "queue";

	/// <summary>Timer-triggered invocation (Azure Functions timer trigger, etc.).</summary>
	public const string Timer = "timer";

}
