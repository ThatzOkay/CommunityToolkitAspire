using Aspire;
using CommunityToolkit.Aspire.InfluxDB.Client;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for connecting InfluxDB database.
/// </summary>
public static class InfluxDBClientExtension
{
    private const string ActivityNameSource = "InfluxDB.Client.DiagnosticSources";

    private const string DefaultConfigSectionName = "Aspire:InfluxDB:Client";

    /// <summary>
    /// Registers <see cref="IInfluxDBClient"/> instance for connecting to an existing or new InfluxDB database with InfluxDB.Client.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> used to add services.</param>
    /// <param name="connectionName">The name used to retrieve the connection string from the "ConnectionStrings" configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It is invoked after the settings are read from the configuration.</param>
    /// <remarks>Notes:
    /// <list type="bullet">
    /// <item><description>Reads the configuration from "Aspire:InfluxDB:Client" section.</description></item>
    /// <item><description>The <see cref="IInfluxDBClient"/> is registered as a singleton.</description></item>
    /// </list>
    /// </remarks>
    public static void AddInfluxDBClient(this IHostApplicationBuilder builder, string connectionName, Action<InfluxDBClientSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        builder.AddInfluxDBClientInternal(DefaultConfigSectionName, connectionName, serviceKey: null, configureSettings: configureSettings);
    }

    /// <summary>
    /// Registers <see cref="IInfluxDBClient"/> instance for connecting to an existing or new InfluxDB database with InfluxDB.Client,
    /// identified by a unique service key.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> used to add services.</param>
    /// <param name="connectionName">The name used to retrieve the connection string from the "ConnectionStrings" configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It is invoked after the settings are read from the configuration.</param>
    /// <remarks>Notes:
    /// <list type="bullet">
    /// <item><description>Reads the configuration from "Aspire:InfluxDB:Client" section.</description></item>
    /// <item><description>The <see cref="IInfluxDBClient"/> is registered as a singleton.</description></item>
    /// </list>
    /// </remarks>
    public static void AddKeyedInfluxDBClient(this IHostApplicationBuilder builder, string connectionName, Action<InfluxDBClientSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        builder.AddInfluxDBClientInternal($"{DefaultConfigSectionName}:{connectionName}", connectionName, serviceKey: connectionName, configureSettings: configureSettings);
    }

    private static void AddInfluxDBClientInternal(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        string connectionName,
        string? serviceKey,
        Action<InfluxDBClientSettings>? configureSettings)
    {
        var settings = new InfluxDBClientSettings();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        if (settings.ConnectionString is null)
        {
            throw new InvalidOperationException(
                    $"InfluxDBClient could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                    $"{nameof(settings.ConnectionString)} must be provided " +
                    $"in the '{configurationSectionName}' configuration section.");
        }

        var httpClientKey = $"{connectionName}_httpClient";
        builder.Services.AddHttpClient(httpClientKey);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(ConfigureInfluxDBClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, serviceKey) => ConfigureInfluxDBClient(sp));
        }

        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(ActivityNameSource));
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "InfluxDB.Client" : $"InfluxDB.Client_{serviceKey}";

            builder.TryAddHealthCheck(
                healthCheckName,
                healthCheck => healthCheck.AddInfluxDB(
                    settings.ConnectionString,
                    healthCheckName,
                    failureStatus: null,
                    tags: null,
                    timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null));
        }

        IInfluxDBClient ConfigureInfluxDBClient(IServiceProvider serviceProvider)
        {
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientKey);

            var client = new InfluxDBClient(new InfluxDBClientOptions(settings.ConnectionString)
            {
                HttpClient = httpClient
            });

            return client;
        }
    }
}
