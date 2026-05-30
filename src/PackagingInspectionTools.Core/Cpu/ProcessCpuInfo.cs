namespace PackagingInspectionTools.Core.Cpu
{
    public sealed class ProcessCpuInfo
    {
        public ProcessCpuInfo(int id, string name, string windowTitle, string priorityClass, string affinityMask, string cpuTime, string workingSet)
        {
            Id = id;
            Name = name;
            WindowTitle = windowTitle;
            PriorityClass = priorityClass;
            AffinityMask = affinityMask;
            CpuTime = cpuTime;
            WorkingSet = workingSet;
        }

        public int Id { get; }

        public string Name { get; }

        public string WindowTitle { get; }

        public string PriorityClass { get; }

        public string AffinityMask { get; }

        public string CpuTime { get; }

        public string WorkingSet { get; }
    }
}
