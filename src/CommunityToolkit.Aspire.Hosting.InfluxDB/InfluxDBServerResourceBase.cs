using InfluxDB.Client.Api.Domain;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.Xml;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Base class for InfluxDB server resources.
/// </summary>
/// <param name="name">The name of the resource</param>
/// <param name="userName">A parameter the contains the InfluxDB username, or <see langword="null"/> to use the default value</param>
/// <param name="password">A parameter the contains the InfluxDB password</param>
/// <param name="initialOrganization">A parameter the contains the initial organization name, or <see langword="null"/> to use the default value</param>
/// <param name="initialBucket">A parameter the contains the initial bucket name, or <see langword="null"/> to use the default value</param>
/// <param name="token">A parameter the contains the InfluxDB token</param>
public abstract class InfluxDBServerResourceBase(string name, ParameterResource? userName, ParameterResource password, ParameterResource? initialOrganization, ParameterResource? initialBucket, ParameterResource token) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const int DefaultHttpPort = 8086;

    internal const string DefaultUsername = "admin";
    internal const string DefaultInitialOrganization = "influxdata";
    internal const string DefaultInitialBucket = "default";
    private EndpointReference? _primaryEndpoint;


    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"{PrimaryEndpoint.Scheme}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}?token={token}");

    /// <summary>
    /// Gets the primary endpoint for the InfluxDB server.
    /// </summary>
    private EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the parameter that contains the InfluxDB username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; } = userName;


    /// <summary>
    /// Gets the parameter that contains the InfluxDB password.
    /// </summary>
    public ParameterResource PasswordParameter { get; } = ThrowIfNull(password);

    /// <summary>
    /// Gets the parameter that contains the InfluxDB initial organization name.
    /// </summary>
    public ParameterResource? InitialOrganizationParameter { get; } = initialOrganization;

    /// <summary>
    /// Gets the parameter that contains the InfluxDB initial bucket name.
    /// </summary>
    public ParameterResource? InitialBucketParameter { get; } = initialBucket;
    
    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null
            ? ReferenceExpression.Create($"{UserNameParameter}")
            : ReferenceExpression.Create($"{DefaultUsername}"); 

    internal ReferenceExpression InitialOrganizationReference =>
        InitialOrganizationParameter is not null
            ? ReferenceExpression.Create($"{InitialOrganizationParameter}")
            : ReferenceExpression.Create($"{DefaultInitialOrganization}");

    internal ReferenceExpression InitialBucketReference =>
        InitialBucketParameter is not null
            ? ReferenceExpression.Create($"{InitialBucketParameter}")
            : ReferenceExpression.Create($"{DefaultInitialBucket}"); 

    private static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}