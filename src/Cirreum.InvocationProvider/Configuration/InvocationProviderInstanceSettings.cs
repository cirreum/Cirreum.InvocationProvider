namespace Cirreum.Invocation.Configuration;

using Cirreum.Providers.Configuration;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Abstract base class for Invocation-provider instance settings — defines common
/// configuration properties shared by all Invocation-family provider instances.
/// Subclasses add source-specific configuration (HubOptions, WebSocketOptions, etc.).
/// </summary>
public abstract class InvocationProviderInstanceSettings
	: IProviderInstanceSettings {

	/// <summary>
	/// Gets or sets a value indicating whether this provider instance is enabled.
	/// When <see langword="false"/>, the instance is skipped during registration.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the endpoint path or address this instance maps to (e.g.
	/// <c>"/chat"</c>, <c>"/voice"</c>). Required for every enabled instance.
	/// </summary>
	public string Path { get; set; } = "";

	/// <summary>
	/// Gets or sets an optional <c>IConfiguration.GetConnectionString(name)</c> lookup key
	/// for instances whose configuration includes secret-bearing fields. ASP.NET's
	/// <c>ConnectionStrings</c> section resolves transparently through SecretsProvider
	/// (KeyVault), local user secrets, environment variables, and appsettings — devs place
	/// the secret under the appropriate layer and reference it by this name.
	/// </summary>
	/// <remarks>
	/// V1 invocation sources (SignalR, raw WebSocket, gRPC) do not require this — their
	/// secrets live with the referenced auth <see cref="Scheme"/>. Included in the base
	/// for forward compatibility with sources that may need transport-level secrets
	/// (e.g., a future webhook ingress with a shared signing key).
	/// </remarks>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets a reference to an Authorization scheme — must match an instance key
	/// under <c>Cirreum:Authorization:Providers:*:Instances:{Scheme}</c>. The invocation
	/// source requires authenticated callers under this scheme. Optional only for sources
	/// that can serve unauthenticated traffic (rare).
	/// </summary>
	public string? Scheme { get; set; }

	/// <summary>
	/// Gets or sets the raw configuration section for impl-specific sub-binding (e.g.
	/// <c>HubOptions</c>, <c>WebSocketOptions</c>). The registrar binds this section onto
	/// the framework's native options class and re-pins authoritative fields after binding.
	/// </summary>
	public IConfigurationSection? Section { get; set; }

}
