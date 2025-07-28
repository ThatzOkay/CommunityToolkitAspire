// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.InfluxDB;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding InfluxDB resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class InfluxDBResourceBuilderExtensions
{
    /// <summary>
    /// Adds a InfluxDB resource to the application model. A container is used for local development.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the resource is added.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="token">The parameter used to provide the operator token for the InfluxDB. If <see langword="null"/> a random token will be generated.</param>
    /// <param name="userName">A name for the initial admin user</param>
    /// <param name="password">A password for the initial admin user</param>
    /// <param name="initialOrganization">A name for the initial organization⁠</param>
    /// <param name="initialBucket">A name for the initial bucket</param>
    /// <param name="port">The host port used when launching the container. If null a random port will be assigned.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the InfluxDB server resource.</returns>
    /// <remarks>
    /// <para>
    /// This resource includes built-in health checks. When this resource is referenced as a dependency
    /// using the <see cref="ResourceBuilderExtensions.WaitFor{T}(IResourceBuilder{T}, IResourceBuilder{IResource})"/>
    /// extension method then the dependent resource will wait until the InfluxDB resource is able to service
    /// requests.
    /// </para>
    /// This version of the package defaults to the <inheritdoc cref="InfluxDBContainerImageTags.Tag"/> tag of the <inheritdoc cref="InfluxDBContainerImageTags.Image"/> container image.
    /// </remarks>
    public static IResourceBuilder<InfluxDBServerResource> AddInfluxDB(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? token = null,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        IResourceBuilder<ParameterResource>? initialOrganization = null,
        IResourceBuilder<ParameterResource>? initialBucket = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var tokenParameter = token?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-token", special: false);
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        var influxDBServer = new InfluxDBServerResource(name, userName?.Resource, passwordParameter, initialOrganization?.Resource, initialBucket?.Resource, tokenParameter);

        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(influxDBServer, async (@event, ct) =>
        {
            connectionString = await influxDBServer.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{influxDBServer.Name}' resource but the connection string was null.");
            }
        });

        var healthCheckKey = $"{name}_check";
        builder.Services
            .AddHealthChecks()
            .AddInfluxDB(
                sp => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"),
                name: healthCheckKey);

        return builder
            .AddResource(influxDBServer)
            .WithEndpoint(port: port, targetPort: InfluxDBServerResource.DefaultHttpPort, scheme: InfluxDBServerResource.PrimaryEndpointName)
            .WithImage(InfluxDBContainerImageTags.Image, InfluxDBContainerImageTags.Tag)
            .WithImageRegistry(InfluxDBContainerImageTags.Registry)
            .WithEnvironment("DOCKER_INFLUXDB_INIT_MODE", "setup")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_USERNAME"] = influxDBServer.UserNameReference;
                context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_PASSWORD"] = influxDBServer.PasswordParameter;
                context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_ORG"] = influxDBServer.InitialOrganizationReference;
                context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_BUCKET"] = influxDBServer.InitialBucketReference;
                context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_ADMIN_TOKEN"] = tokenParameter;
            })
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data directory to a InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the InfluxDB server.</param>
    /// <param name="name">Optional name for the volume. Defaults to a generated name if not provided.</param>
    /// <param name="isReadOnly">Indicates whether the volume should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the InfluxDB server resource.</returns>
    public static IResourceBuilder<InfluxDBServerResource> WithDataVolume(this IResourceBuilder<InfluxDBServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "influxdb2-data"), "/var/lib/influxdb2", isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data directory to a InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the InfluxDB server.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">Indicates whether the bind mount should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the InfluxDB server resource.</returns>
    public static IResourceBuilder<InfluxDBServerResource> WithDataBindMount(this IResourceBuilder<InfluxDBServerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/influxdb2", isReadOnly);
    }

    /// <summary>
    /// Adds a named volume for the configuration directory to a InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the InfluxDB server.</param>
    /// <param name="name">Optional name for the volume. Defaults to a generated name if not provided.</param>
    /// <param name="isReadOnly">Indicates whether the volume should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the InfluxDB server resource.</returns>
    public static IResourceBuilder<InfluxDBServerResource> WithConfigVolume(this IResourceBuilder<InfluxDBServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "influxdb2-config"), "/etc/influxdb2", isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the configuration directory to a InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the InfluxDB server.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">Indicates whether the bind mount should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the InfluxDB server resource.</returns>
    public static IResourceBuilder<InfluxDBServerResource> WithConfigBindMount(this IResourceBuilder<InfluxDBServerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/etc/influxdb2", isReadOnly);
    }
}
