namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for InfluxDB container.
/// </summary>
public class InfluxDBServerResource(string name, ParameterResource token) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const int DefaultHttpPort = 8086;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the InfluxDB server.
    /// </summary>
    private EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the connection string expression for the InfluxDB server,
    /// formatted as "http://{host}:{port}?token={token}".
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"{PrimaryEndpoint.Scheme}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}?token={token}");
}
