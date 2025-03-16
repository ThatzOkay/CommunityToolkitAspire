namespace CommunityToolkit.Aspire.InfluxDB.Client;

/// <summary>
/// Provides the client configuration settings for connecting to a InfluxDB database.
/// </summary>
public sealed class InfluxDBClientSettings
{
    /// <summary>
    /// The connection string of the InfluxDB server.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether InfluxDB health check is disabled or not.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets the timeout in milliseconds for the InfluxDB health check.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry tracing is disabled.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool DisableTracing { get; set; }
}
