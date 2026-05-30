namespace PackagingInspectionTools.Core.Gpu
{
    public sealed class GpuInfo
    {
        public GpuInfo(
            string index,
            string name,
            string driverVersion,
            string utilization,
            string memoryUsed,
            string memoryTotal,
            string temperature,
            string powerDraw,
            string powerLimit,
            string graphicsClock,
            string memoryClock,
            string computeMode,
            string source)
        {
            Index = index;
            Name = name;
            DriverVersion = driverVersion;
            Utilization = utilization;
            MemoryUsed = memoryUsed;
            MemoryTotal = memoryTotal;
            Temperature = temperature;
            PowerDraw = powerDraw;
            PowerLimit = powerLimit;
            GraphicsClock = graphicsClock;
            MemoryClock = memoryClock;
            ComputeMode = computeMode;
            Source = source;
        }

        public string Index { get; }
        public string Name { get; }
        public string DriverVersion { get; }
        public string Utilization { get; }
        public string MemoryUsed { get; }
        public string MemoryTotal { get; }
        public string Temperature { get; }
        public string PowerDraw { get; }
        public string PowerLimit { get; }
        public string GraphicsClock { get; }
        public string MemoryClock { get; }
        public string ComputeMode { get; }
        public string Source { get; }
    }
}
