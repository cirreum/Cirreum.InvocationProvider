namespace Cirreum.Invocation.Configuration;

using Cirreum.Providers.Configuration;

/// <summary>
/// Abstract base class for Invocation-provider settings — contains a collection of
/// per-instance configurations bound from
/// <c>Cirreum:Invocation:Providers:{ProviderName}</c>.
/// </summary>
/// <typeparam name="TInstanceSettings">The type of individual instance settings for this provider.</typeparam>
public abstract class InvocationProviderSettings<TInstanceSettings>
	: IProviderSettings<TInstanceSettings>
		where TInstanceSettings : InvocationProviderInstanceSettings {

	/// <summary>
	/// Gets or sets the collection of provider instance settings keyed by instance name.
	/// Each instance represents a separately configured invocation source endpoint — for
	/// example, multiple SignalR hubs at different paths or multiple WebSocket endpoints.
	/// </summary>
	public Dictionary<string, TInstanceSettings> Instances { get; set; } = [];

}
