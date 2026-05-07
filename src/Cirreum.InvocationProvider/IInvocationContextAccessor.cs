namespace Cirreum.Invocation;

/// <summary>
/// Ambient accessor for the current <see cref="IInvocationContext"/>. Returns
/// <see langword="null"/> outside an active invocation scope.
/// </summary>
/// <remarks>
/// <para>
/// Single ambient seam for every invocation source. Source adapters call
/// <see cref="Set"/> at the inbound seam and <see cref="Clear"/> on exit (paired
/// in a try/finally). Consumers read via <see cref="Current"/>.
/// </para>
/// <para>
/// Naming mirrors <c>IAuthorizationContextAccessor.Current</c> for consistency
/// across Cirreum's progressively-enriched context chain.
/// </para>
/// </remarks>
public interface IInvocationContextAccessor {

	/// <summary>Gets the current invocation, or <see langword="null"/> if none is active.</summary>
	/// <remarks>
	/// AsyncLocal semantics — flows naturally into <c>await</c>-continuations and
	/// <c>Task.Run</c> when ExecutionContext is captured.
	/// </remarks>
	IInvocationContext? Current { get; }

	/// <summary>Sets the ambient invocation for the current async flow.</summary>
	/// <remarks>
	/// Stomps any existing value silently — adapters are expected to clear before
	/// setting in nested scenarios. Adapters are responsible for paired
	/// <see cref="Set"/>/<see cref="Clear"/> calls.
	/// </remarks>
	void Set(IInvocationContext invocation);

	/// <summary>Clears the ambient invocation. Safe to call when none is set (no-op).</summary>
	void Clear();

}
