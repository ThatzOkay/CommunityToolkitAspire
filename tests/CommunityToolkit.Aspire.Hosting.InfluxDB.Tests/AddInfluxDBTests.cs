using Aspire.Hosting;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.InfluxDB.Tests;

public class AddInfluxDBTests
{
    [Fact]
    public async Task AddInfluxDBContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var tokenParameter = appBuilder.AddParameter("token", "my-secret-token");
        var influxdb = appBuilder.AddInfluxDB("influxdb", token: tokenParameter);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());
        Assert.Equal("influxdb", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(InfluxDBServerResource.DefaultHttpPort, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(InfluxDBContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(InfluxDBContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(InfluxDBContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await influxdb.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_MODE", env.Key);
                Assert.Equal("setup", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_USERNAME", env.Key);
                Assert.Equal("admin", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_PASSWORD", env.Key);
                Assert.NotEmpty(env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ORG", env.Key);
                Assert.Equal("influxdata", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_BUCKET", env.Key);
                Assert.Equal("default", env.Value);
            },
            env =>
            {
                Assert.Equal("DOCKER_INFLUXDB_INIT_ADMIN_TOKEN", env.Key);
                Assert.Equal("my-secret-token", env.Value);
            });
    }

    [Fact]
    public async Task InfluxDBCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var tokenParameter = appBuilder.AddParameter("token", "my-secret-token");
        var influxdb = appBuilder
            .AddInfluxDB("influxdb", token: tokenParameter)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 18086));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal("http://localhost:18086?token=my-secret-token", connectionString);
        Assert.Equal("http://{influxdb.bindings.http.host}:{influxdb.bindings.http.port}?token={token.value}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void VerifyNonDefaultImageTag()
    {
        const string tag = "2.7";

        var builder = DistributedApplication.CreateBuilder();
        builder.AddInfluxDB("influxdb").WithImageTag(tag);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);
        Assert.NotNull(annotation.Tag);
        Assert.Equal(tag, annotation.Tag);
    }

    [Fact]
    public void VerifyCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddInfluxDB("influxdb", port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(12345, endpoint.Port);
    }

    [Fact]
    public void SpecifiedDataVolumeNameIsUsed()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddInfluxDB("influxdb").WithDataVolume("data");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("data", annotation.Source);
    }

    [Theory]
    [InlineData("data")]
    [InlineData(null)]
    public void CorrectTargetPathOnDataVolumeMount(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddInfluxDB("influxdb").WithDataVolume(volumeName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("/var/lib/influxdb2", annotation.Target);
    }

    [Theory]
    [InlineData("config")]
    [InlineData(null)]
    public void CorrectTargetPathOnConfigVolumeMount(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddInfluxDB("influxdb").WithConfigVolume(volumeName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("/etc/influxdb2", annotation.Target);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadOnlyDataVolumeMount(bool isReadOnly)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddInfluxDB("influxdb").WithDataVolume(isReadOnly: isReadOnly);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal(isReadOnly, annotation.IsReadOnly);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadOnlyConfigVolumeMount(bool isReadOnly)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddInfluxDB("influxdb").WithConfigVolume(isReadOnly: isReadOnly);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal(isReadOnly, annotation.IsReadOnly);
    }
}