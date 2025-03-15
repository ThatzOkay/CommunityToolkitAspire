﻿using HealthChecks.InfluxDB;
using InfluxDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.InfluxDB;

internal static class HealthChecksExtensions
{
    private const string NAME = "influxdb";

    /// <summary>
    /// Add a health check for InfluxDB server services.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionStringFactory">A factory to build the connection string to use.</param>
    /// <param name="name">The health check name. Optional. If <c>null</c> the type name 'influxdb' will be used for the name.</param>
    /// <param name="failureStatus">that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddInfluxDB(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, string> connectionStringFactory,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp =>
            {
                var connectionString = ValidateConnectionString(connectionStringFactory, sp);
                return new InfluxDBHealthCheck(builder =>
                {
                    return new InfluxDBClientOptions(connectionString);
                });
            },
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Validates that the connection string is not null or empty.
    /// </summary>
    /// <param name="connectionStringFactory">The factory to generate the connection string.</param>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <returns>A valid, non-empty connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection string is null or empty.</exception>
    private static string ValidateConnectionString(Func<IServiceProvider, string> connectionStringFactory, IServiceProvider serviceProvider)
    {
        var connectionString = connectionStringFactory(serviceProvider);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Failed to generate a valid InfluxDB connection string. The result cannot be null or empty.");
        }

        return connectionString;
    }
}
