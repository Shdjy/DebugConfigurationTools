namespace PackagingInspectionTools.Core.Cpu
{
    public sealed class CpuCoreInfo
    {
        public CpuCoreInfo(int logicalProcessorIndex, byte efficiencyClass, bool isPerformanceCore, long affinityMask)
        {
            LogicalProcessorIndex = logicalProcessorIndex;
            EfficiencyClass = efficiencyClass;
            IsPerformanceCore = isPerformanceCore;
            AffinityMask = affinityMask;
        }

        public int LogicalProcessorIndex { get; }

        public byte EfficiencyClass { get; }

        public bool IsPerformanceCore { get; }

        public long AffinityMask { get; }

        public string CoreTypeText
        {
            get { return IsPerformanceCore ? "高性能核心" : "低功耗核心"; }
        }

        public string AffinityMaskText
        {
            get { return "0x" + AffinityMask.ToString("X"); }
        }
    }
}
