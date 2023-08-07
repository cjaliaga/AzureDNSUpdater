using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.Identity;
using System.Net;
using Microsoft.Extensions.Options;
using Azure.ResourceManager.Dns.Models;

namespace AzureDNSUpdater;

public class TimedDnsZoneUpdater : BackgroundService
{
    private readonly ILogger<TimedDnsZoneUpdater> _logger;
    private readonly IOptions<TimedDnsZoneUpdaterOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public TimedDnsZoneUpdater(ILogger<TimedDnsZoneUpdater> logger, IOptions<TimedDnsZoneUpdaterOptions> options, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Azure DNS Zone Updater is running");
        _logger.LogInformation("Subscription: {subscriptionId}", _options.Value.SubscriptionId);
        _logger.LogInformation("Resource Group: {resourceGroup}", _options.Value.ResourceGroup);
        _logger.LogInformation("Domain: {domain}", _options.Value.Domain);

        await DoWork(stoppingToken);

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.Value.Interval));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWork(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Azure DNS Zone Updater is stopping.");
        }
    }

    protected async Task DoWork(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        ArmClient armClient = new ArmClient(new ClientSecretCredential(options.TenantId, options.AppId, options.AppSecret));
        SubscriptionResource subscription = await armClient.GetSubscriptions().GetAsync(options.SubscriptionId, cancellationToken);
        
        // Get the zone
        ResourceGroupResource resourceGroup = await subscription.GetResourceGroupAsync(options.ResourceGroup, cancellationToken);
        DnsZoneResource dnsZone = await resourceGroup.GetDnsZoneAsync(options.Domain, cancellationToken);
        var currentIp = await GetCurrentIPAddressAsync(cancellationToken);
        _logger.LogInformation("Current IP Address: {currentIp}", currentIp);

        foreach (var record in options.GetRecords())
        {
            var hostNameIp = await GetHostNameCurrentIPAddressAsync(record.HostName, cancellationToken);
            _logger.LogInformation("Current IP Address for {hostName}: {hostNameIp}", record.HostName, hostNameIp);

            if(!currentIp.Equals(hostNameIp))
            {
                _logger.LogInformation("Updating record: {record} to {hostNameIp}", record.Name, currentIp);
                DnsARecordResource dnsRecord = await dnsZone.GetDnsARecordAsync(record.Name, cancellationToken);
                dnsRecord.Data.DnsARecords.Clear();
                dnsRecord.Data.DnsARecords.Add(new DnsARecordInfo() { IPv4Address = currentIp});
                dnsRecord.Data.TtlInSeconds = options.Interval;
                await dnsRecord.UpdateAsync(dnsRecord.Data, cancellationToken: cancellationToken);
            }
            else
            {
                _logger.LogInformation("Record {record} is up to date.", record.Name);
            }
        }
    }

    protected async Task<IPAddress> GetCurrentIPAddressAsync(CancellationToken cancellationToken)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        string response = await httpClient.GetStringAsync("https://api.ipify.org", cancellationToken);
        return IPAddress.Parse(response);
    }

    protected async Task<IPAddress?> GetHostNameCurrentIPAddressAsync(string hostName, CancellationToken cancellationToken)
    {
        var ips = await Dns.GetHostAddressesAsync(hostName, cancellationToken);
        return ips.FirstOrDefault();
    }
}