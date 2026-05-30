using PackagingInspectionTools.Core.Network;
using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Cpu
{
    public interface ICpuService
    {
        CpuSummary GetSummary();

        IReadOnlyList<ProcessCpuInfo> GetProcesses();

        OperationResult SetHighPerformancePowerPlan();

        OperationResult ApplyLowLatencyPowerSettings();

        OperationResult SetProcessHighPriority(int processId);

        OperationResult RestrictProcessToPerformanceCores(int processId);

        OperationResult RestoreProcessToAllCores(int processId);
    }
}
