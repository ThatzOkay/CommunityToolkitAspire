﻿using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.InfluxDB.Client.Tests;

public class AspireInfluxDBExtensionsTests(InfluxDBContainerFixture serverFixture) : IClassFixture<InfluxDBContainerFixture>
{
    private const string DefaultConnectionName = "InfluxDB";

    private string DefaultConnectionString => serverFixture.GetContainerEndpoint() ??
        throw new InvalidOperationException("The server was not initialized.");

    [Fact]
    public void AddKeyedInfluxDBClient_IsRegistered()
    {
        var builder = CreateBuilder();

        Action<InfluxDBClientSettings>? configSettings = null;

        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: configSettings);
        using var host = builder.Build();

        using var client = host.Services.GetRequiredKeyedService<IInfluxDBClient>(DefaultConnectionName);
        Assert.NotNull(client);
    }

    [Fact]
    public void AddInfluxDBClient_IsRegisteredWithSettings()
    {
        var builder = CreateBuilder();

        var settings = GetDefaultSettings(DefaultConnectionString);

        builder.AddInfluxDBClient(settings: settings);
        using var host = builder.Build();

        using var client = host.Services.GetRequiredService<IInfluxDBClient>();
        Assert.NotNull(client);

    }

    [Fact]
    public void AddKeyedInfluxDBClient_IsRegisteredWithSettings()
    {
        var builder = CreateBuilder();

        var settings = GetDefaultSettings(DefaultConnectionString);

        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, settings: settings);
        using var host = builder.Build();

        using var client = host.Services.GetRequiredKeyedService<IInfluxDBClient>(DefaultConnectionName);
        Assert.NotNull(client);
    }

    [Fact]
    public void AddKeyedInfluxDBClient_ClientsAreNotEqual()
    {
        var builder = CreateBuilder();
        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
        });
        using var host = builder.Build();

        using var client1 = host.Services.GetRequiredKeyedService<IInfluxDBClient>(DefaultConnectionName);
        using var client2 = host.Services.GetRequiredKeyedService<IInfluxDBClient>(DefaultConnectionName);

        Assert.NotEqual(client1, client2);
    }

    [Fact]
    public void AddInfluxDBClient_ClientsAreNotEqual()
    {
        var builder = CreateBuilder();
        builder.AddInfluxDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
        });
        using var host = builder.Build();

        using var client1 = host.Services.GetRequiredService<IInfluxDBClient>();
        using var client2 = host.Services.GetRequiredService<IInfluxDBClient>();

        Assert.NotEqual(client1, client2);
    }

    [Fact]
    public void AddKeyedInfluxDBClient_GetRequiredServiceThrows()
    {
        var builder = CreateBuilder();
        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: settings =>
        {
        });
        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<IInfluxDBClient>());
    }

    [Fact]
    public void AddInfluxDBClient_MissingConnectionStringThrows()
    {
        var builder = CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
        {
            builder.AddInfluxDBClient(connectionName: DefaultConnectionName, configureSettings: settings =>
            {
                settings.ConnectionString = null;
            });
        });
    }

    [Fact]
    public void AddInfluxDBClient_InvalidConnectionStringThrows()
    {
        var builder = CreateBuilder();

        Assert.Throws<ArgumentException>(() =>
        {
            builder.AddInfluxDBClient(connectionName: DefaultConnectionName, configureSettings: settings =>
            {
                settings.ConnectionString = "tcp://invalid";
            });
        });
    }

    [Fact]
    public void AddKeyedInfluxDBClient_ClientsAreNotEqualWithMultipleRegistrations()
    {
        var connectionName1 = Guid.NewGuid().ToString("N");
        var connectionName2 = Guid.NewGuid().ToString("N");

        IEnumerable<KeyValuePair<string, string?>> config =
        [
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName1}", DefaultConnectionString),
            new KeyValuePair<string, string?>($"ConnectionStrings:{connectionName2}", DefaultConnectionString)
        ];
        var builder = CreateBuilder(config);

        builder.AddKeyedInfluxDBClient(serviceKey: connectionName1, connectionName: connectionName1, configureSettings: clientSettings =>
        {
        });
        builder.AddKeyedInfluxDBClient(serviceKey: connectionName2, connectionName: connectionName2, configureSettings: clientSettings =>
        {
        });

        using var host = builder.Build();

        using var client1 = host.Services.GetRequiredKeyedService<IInfluxDBClient>(connectionName1);
        using var client2 = host.Services.GetRequiredKeyedService<IInfluxDBClient>(connectionName2);

        Assert.NotEqual(client1, client2);
    }

    [Fact]
    public async Task AddInfluxDBClient_HealthCheckIsEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddInfluxDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = "InfluxDB.Client";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void AddInfluxDBClient_HealthCheckIsDisabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddInfluxDBClient(connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.DisableHealthChecks = true;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    [Fact]
    public async Task AddKeyedInfluxDBClient_HealthCheckIsEnabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        var healthCheckReport = await healthCheckService.CheckHealthAsync();

        var healthCheckName = $"InfluxDB.Client_{DefaultConnectionName}";

        Assert.Contains(healthCheckReport.Entries, x => x.Key == healthCheckName);
    }

    [Fact]
    public void AddKeyedInfluxDBClient_HealthCheckIsDisabled()
    {
        var databaseName = Guid.NewGuid().ToString("N");

        var builder = CreateBuilder();

        builder.AddKeyedInfluxDBClient(serviceKey: DefaultConnectionName, connectionName: DefaultConnectionName, configureSettings: clientSettings =>
        {
            clientSettings.DisableHealthChecks = true;
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetService<HealthCheckService>();

        Assert.Null(healthCheckService);
    }

    private HostApplicationBuilder CreateBuilder(IEnumerable<KeyValuePair<string, string?>>? config = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection(config ?? GetDefaultConfiguration());
        return builder;
    }

    private IEnumerable<KeyValuePair<string, string?>> GetDefaultConfiguration() =>
    [
        new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", DefaultConnectionString)
    ];

    private static InfluxDBClientSettings GetDefaultSettings(string connectionString)
    {
        return new InfluxDBClientSettings
        {
            ConnectionString = connectionString
        };
    }
}
