namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource for InfluxDB container.
/// </summary>
public class InfluxDBServerResource(string name, ParameterResource? userName, ParameterResource password, ParameterResource? initialOrganization, ParameterResource? initialBucket,  ParameterResource token) : InfluxDBServerResourceBase(name, userName, password, initialOrganization, initialBucket, token), IResourceWithConnectionString
{
}
