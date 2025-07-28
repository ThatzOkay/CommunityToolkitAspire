using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.InfluxDB.Tests;


[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_InfluxDB_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_InfluxDB_AppHost>>
{
    [Fact]
    public async Task TestAppHost()
    {
        using var cancellationToken = new CancellationTokenSource();
        cancellationToken.CancelAfter(TimeSpan.FromMinutes(5));

        var connectionName = "influxdb";

        await fixture.ResourceNotificationService.WaitForResourceAsync(connectionName, KnownResourceStates.Running, cancellationToken.Token).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken.Token);

        var endpoint = fixture.GetEndpoint(connectionName, "http");
        Assert.NotNull(endpoint);
        Assert.False(string.IsNullOrWhiteSpace(endpoint.OriginalString));
        Assert.True(endpoint.Scheme == Uri.UriSchemeHttp);

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var serverResource = Assert.Single(appModel.Resources.OfType<InfluxDBServerResource>());

        var serverConnectionString = await serverResource.ConnectionStringExpression.GetValueAsync(cancellationToken.Token);
        Assert.False(string.IsNullOrWhiteSpace(serverConnectionString));
        Assert.Contains(endpoint.OriginalString, serverConnectionString);

        // Create InfluxDB Client

        var clientBuilder = Host.CreateApplicationBuilder();
        clientBuilder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName}", serverConnectionString)
        ]);

        clientBuilder.AddInfluxDBClient(connectionName);
        var host = clientBuilder.Build();

        using var client = host.Services.GetRequiredService<IInfluxDBClient>();

        var writeApi = client.GetWriteApiAsync();
        var point = PointData.Measurement("altitude")
                    .Tag("plane", "test-plane")
                    .Field("value", 1234)
                    .Timestamp(new DateTime(2025, 02, 01, 00, 00, 00, DateTimeKind.Utc), WritePrecision.Ns);

        await writeApi.WritePointAsync(point, "default", "influxdata");

        var queryApi = client.GetQueryApi();
        var tables = await queryApi.QueryAsync("from(bucket:\"default\") |> range(start: 0)", "influxdata");
        var results = tables.SelectMany(table => table.Records.Select(record => new AltitudeModel
        {
            Time = record.GetTimeInDateTime(),
            Altitude = int.Parse(record.GetValue().ToString()!)
        }))
        .ToList();

        var result = Assert.Single(results);
        Assert.Equal(new DateTime(2025, 02, 01, 00, 00, 00, DateTimeKind.Utc), result.Time);
        Assert.Equal(1234, result.Altitude);
    }

    private class AltitudeModel
    {
        public required DateTime? Time { get; init; }
        public required int Altitude { get; init; }
    }
}
