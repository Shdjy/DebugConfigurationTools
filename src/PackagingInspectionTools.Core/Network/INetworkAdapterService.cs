using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Network
{
    public interface INetworkAdapterService
    {
        IReadOnlyList<NetworkAdapterInfo> GetAdapters();

        OperationResult SetAdvancedProperty(NetworkSettingUpdate update);

        OperationResult ApplyGigECameraPreset(string adapterId);

        OperationResult RestartAdapter(string adapterName);
    }
}
