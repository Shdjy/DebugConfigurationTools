using PackagingInspectionTools.Core.Network;
using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Gpu
{
    public interface IGpuService
    {
        IReadOnlyList<GpuInfo> GetGpus();

        OperationResult EnablePersistenceMode(string gpuIndex);

        OperationResult SetPowerLimit(string gpuIndex, int watts);

        OperationResult LockGraphicsClock(string gpuIndex, int minMhz, int maxMhz);

        OperationResult ResetGraphicsClock(string gpuIndex);

        OperationResult SetComputeMode(string gpuIndex, string mode);

        OperationResult SetDriverModel(string gpuIndex, bool tccMode);

        OperationResult DisablePcieLinkStatePowerManagement();
    }
}
