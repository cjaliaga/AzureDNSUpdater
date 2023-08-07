namespace AzureDNSUpdater
{
    public class TimedDnsZoneUpdaterOptions
    {
        private List<DnsRecord>? _records;

        public string AppId { get; set; }
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string AppSecret { get; set; }
        public string Domain { get; set; }
        public string ResourceGroup { get; set; }
        public string RecordSet { get; set; }
        public int Interval { get; set; }

        public List<DnsRecord> GetRecords()
        {
            _records ??= RecordSet.Split(",", StringSplitOptions.RemoveEmptyEntries)
                .Select(record =>
                {
                    if (record == "@")
                    {
                        return new DnsRecord(record, Domain);
                    }
                    return new DnsRecord(record, $"{record}.{Domain}");
                }).ToList();

            return _records;
        }
    }

    public record DnsRecord(string Name, string HostName);
}