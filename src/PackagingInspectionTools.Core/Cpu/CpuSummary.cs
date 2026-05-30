using System.Collections.Generic;

namespace PackagingInspectionTools.Core.Cpu
{
    public sealed class CpuSummary
    {
        public CpuSummary(string processorName, int logicalProcessorCount, IReadOnlyList<CpuCoreInfo> cores, string activePowerScheme)
        {
            ProcessorName = processorName;
            LogicalProcessorCount = logicalProcessorCount;
            Cores = cores;
            ActivePowerScheme = activePowerScheme;
        }

        public string ProcessorName { get; }

        public int LogicalProcessorCount { get; }

        public IReadOnlyList<CpuCoreInfo> Cores { get; }

        public string ActivePowerScheme { get; }

        public bool HasHybridCores
        {
            get
            {
                if (Cores.Count == 0)
                {
                    return false;
                }

                var first = Cores[0].EfficiencyClass;
                foreach (var core in Cores)
                {
                    if (core.EfficiencyClass != first)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
