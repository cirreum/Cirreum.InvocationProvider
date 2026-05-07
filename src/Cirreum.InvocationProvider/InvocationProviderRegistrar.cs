namespace Cirreum.Invocation;

using Cirreum.Invocation.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Abstract base class for implementing Invocation-family provider registrars that
/// handle the registration and configuration of inbound invocation sources within the
/// dependency injection container and the HTTP endpoint route builder.
/// </summary>
/// <typeparam name="TSettings">The type of provider settings that contains multiple instances.</typeparam>
/// <typeparam name="TInstanceSettings">The type of individual instance settings for this provider.</typeparam>
/// <remarks>
/// <para>
/// Invocation-source providers differ from Authorization providers in that they must
/// register both DI services (services phase, before <c>builder.Build()</c>) and HTTP
/// endpoints (endpoints phase, after <c>builder.Build()</c>). This base class therefore
/// exposes two registration phases: <see cref="Register"/> for the services phase and
/// <see cref="Map"/> for the endpoints phase.
/// </para>
/// <para>
/// The instance key under <c>Instances:</c> in <c>appsettings.json</c> is the logical
/// instance name. Unlike Identity (which auto-derives <c>Source</c>) and Authorization
/// (which auto-derives <c>Scheme</c>), the Invocation family does not auto-derive any
/// field from the instance key — the source endpoint's identity is its
/// <see cref="InvocationProviderInstanceSettings.Path"/>, and its
/// <see cref="InvocationProviderInstanceSettings.Scheme"/> references an external
/// Authorization instance.
/// </para>
/// </remarks>
public abstract class InvocationProviderRegistrar<TSettings, TInstanceSettings>
	: IProviderRegistrar<TSettings, TInstanceSettings>
		where TInstanceSettings : InvocationProviderInstanceSettings
		where TSettings : InvocationProviderSettings<TInstanceSettings> {

	private static readonly Dictionary<string, string> ProcessedInstances = [];

	/// <inheritdoc/>
	public ProviderType ProviderType => ProviderType.Invocation;

	/// <inheritdoc/>
	public abstract string ProviderName { get; }

	/// <summary>
	/// Validates provider-instance settings. Override to implement provider-specific
	/// validation beyond the common <see cref="InvocationProviderInstanceSettings.Path"/>
	/// check performed by the base <see cref="RegisterInstance"/>.
	/// </summary>
	/// <param name="settings">The instance settings to validate.</param>
	public virtual void ValidateSettings(TInstanceSettings settings) {
	}

	/// <summary>
	/// Registers DI services for all enabled instances in <paramref name="providerSettings"/>.
	/// Called during the services phase (before <c>builder.Build()</c>) by the
	/// provider-specific <c>Add*</c> extension on <c>IInvocationBuilder</c>.
	/// </summary>
	/// <param name="providerSettings">Populated provider settings (bound from configuration).</param>
	/// <param name="services">The DI service collection.</param>
	/// <param name="configuration">The root configuration object.</param>
	public virtual void Register(
		TSettings providerSettings,
		IServiceCollection services,
		IConfiguration configuration) {

		if (providerSettings is null || providerSettings.Instances.Count == 0) {
			return;
		}

		foreach (var (key, settings) in providerSettings.Instances) {
			if (!settings.Enabled) {
				continue;
			}

			this.RegisterInstance(key, settings, services, configuration);
		}
	}

	/// <summary>
	/// Registers DI services for a single provider instance. Performs duplicate-registration
	/// detection, validates that <see cref="InvocationProviderInstanceSettings.Path"/> is
	/// set, runs provider-specific validation, and delegates instance-specific service
	/// registration to <see cref="RegisterSource"/>.
	/// </summary>
	/// <param name="key">The configuration instance key.</param>
	/// <param name="settings">The instance settings.</param>
	/// <param name="services">The DI service collection.</param>
	/// <param name="configuration">The root configuration object.</param>
	public virtual void RegisterInstance(
		string key,
		TInstanceSettings settings,
		IServiceCollection services,
		IConfiguration configuration) {

		// Guard against duplicate registration of the same instance key across multiple calls
		var providerRegistrationKey = $"Cirreum.{this.ProviderType}.{this.ProviderName}::{key}";
		if (!ProcessedInstances.TryAdd(providerRegistrationKey, $"{settings.GetHashCode()}")) {
			throw new InvalidOperationException(
				$"An invocation source instance with key '{key}' for provider '{this.ProviderName}' has already been registered.");
		}

		// Must have settings
		if (settings is null) {
			throw new InvalidOperationException($"Missing required settings for invocation source instance '{key}'.");
		}

		// Every invocation source instance must expose a path
		if (string.IsNullOrWhiteSpace(settings.Path)) {
			throw new InvalidOperationException(
				$"Invocation source provider '{this.ProviderName}' instance '{key}' requires a Path.");
		}

		// Capture the raw section onto settings for impl-specific sub-binding (HubOptions, etc.)
		settings.Section = configuration.GetSection(this.GetInstanceSectionPath(key));

		// Provider-specific validation
		this.ValidateSettings(settings);

		// Delegate instance-specific DI registration to derived class
		this.RegisterSource(key, settings, services, configuration);
	}

	/// <summary>
	/// Maps HTTP endpoints for all enabled instances in <paramref name="providerSettings"/>.
	/// Called during the endpoints phase (after <c>builder.Build()</c>) by
	/// <c>app.MapInvocationEndpoints()</c>.
	/// </summary>
	/// <param name="providerSettings">Populated provider settings (bound from configuration).</param>
	/// <param name="endpoints">The endpoint route builder.</param>
	public virtual void Map(
		TSettings providerSettings,
		IEndpointRouteBuilder endpoints) {

		if (providerSettings is null || providerSettings.Instances.Count == 0) {
			return;
		}

		foreach (var (key, settings) in providerSettings.Instances) {
			if (!settings.Enabled) {
				continue;
			}

			this.MapSource(key, settings, endpoints);
		}
	}

	/// <summary>
	/// Gets the configuration section path for a specific instance.
	/// </summary>
	/// <param name="instanceKey">The instance key.</param>
	/// <returns>The configuration section path, e.g. <c>Cirreum:Invocation:Providers:SignalR:Instances:chat</c>.</returns>
	protected string GetInstanceSectionPath(
		string instanceKey) =>
		$"Cirreum:{this.ProviderType}:Providers:{this.ProviderName}:Instances:{instanceKey}";

	/// <summary>
	/// Registers DI services for a single enabled instance. Derived classes implement
	/// this to register their source adapter, populate the per-instance
	/// <see cref="IInvocationContext"/> wiring, register the per-instance impl as a
	/// keyed scoped service in DI, and register any provider-specific collaborators
	/// (HubFilter, frame handlers, gRPC interceptors, etc.).
	/// </summary>
	/// <param name="key">The configuration instance key.</param>
	/// <param name="settings">The instance settings.</param>
	/// <param name="services">The DI service collection.</param>
	/// <param name="configuration">The root configuration object.</param>
	protected abstract void RegisterSource(
		string key,
		TInstanceSettings settings,
		IServiceCollection services,
		IConfiguration configuration);

	/// <summary>
	/// Maps the HTTP endpoint(s) for a single enabled instance. Derived classes implement
	/// this to call <c>endpoints.MapHub&lt;T&gt;</c>, raw-WS endpoint mapping, gRPC service
	/// mapping, etc., and to attach the auth scheme via
	/// <c>RequireAuthorization(...)</c> when <see cref="InvocationProviderInstanceSettings.Scheme"/>
	/// is specified.
	/// </summary>
	/// <param name="key">The configuration instance key.</param>
	/// <param name="settings">The instance settings.</param>
	/// <param name="endpoints">The endpoint route builder.</param>
	protected abstract void MapSource(
		string key,
		TInstanceSettings settings,
		IEndpointRouteBuilder endpoints);

}
