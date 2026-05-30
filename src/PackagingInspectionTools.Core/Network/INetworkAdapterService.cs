using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Network
{
    public interface INetworkAdapterService
    {
        IReadOnlyList<NetworkAdapterInfo> GetAdapters();

        OperationResult SetAdvancedProperty(NetworkSettingUpdate update);

        OperationResult ApplyGigECameraPreset(string adapterId);

        OperationResult RestartAdapter(string adapterName);

        OperationResult SetStaticIPv4Address(string adapterName, string ipAddress, string subnetMask);

        OperationResult EnableDhcpIPv4(string adapterName);

        OperationResult Ping(NetworkPingRequest request);

        OperationResult Ping(NetworkPingRequest request, System.Action<string> outputReceived);
    }
}
