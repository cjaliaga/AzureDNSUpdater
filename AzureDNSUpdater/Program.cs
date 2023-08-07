using AzureDNSUpdater;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<TimedDnsZoneUpdater>();
        services.AddHttpClient();
        services.Configure<TimedDnsZoneUpdaterOptions>(context.Configuration.GetSection("DNSUpdater"));
    })
    .Build();

host.Run();
