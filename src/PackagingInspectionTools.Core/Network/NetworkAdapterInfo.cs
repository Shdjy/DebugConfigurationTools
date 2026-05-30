using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Network
{
    public sealed class NetworkAdapterInfo
    {
        public NetworkAdapterInfo(
            string id,
            string name,
            string description,
            string registryPath,
            string operationalStatus,
            string adapterType,
            string macAddress,
            long speedBitsPerSecond,
            IReadOnlyList<AdapterAdvancedProperty> advancedProperties)
        {
            Id = id;
            Name = name;
            Description = description;
            RegistryPath = registryPath;
            OperationalStatus = operationalStatus;
            AdapterType = adapterType;
            MacAddress = macAddress;
            SpeedBitsPerSecond = speedBitsPerSecond;
            AdvancedProperties = advancedProperties;
        }

        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        public string RegistryPath { get; }

        public string OperationalStatus { get; }

        public string AdapterType { get; }

        public string MacAddress { get; }

        public long SpeedBitsPerSecond { get; }

        public IReadOnlyList<AdapterAdvancedProperty> AdvancedProperties { get; }

        public string SpeedText
        {
            get
            {
                if (SpeedBitsPerSecond <= 0)
                {
                    return "Unknown";
                }

                var mbps = SpeedBitsPerSecond / 1_000_000D;
                return mbps >= 1000 ? $"{mbps / 1000:0.##} Gbps" : $"{mbps:0.##} Mbps";
            }
        }
    }
}
