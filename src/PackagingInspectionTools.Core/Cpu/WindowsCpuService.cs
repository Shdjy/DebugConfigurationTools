using Microsoft.Win32;
using PackagingInspectionTools.Core.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PackagingInspectionTools.Core.Cpu
{
    public sealed class WindowsCpuService : ICpuService
    {
        private const int RelationProcessorCore = 0;

        public CpuSummary GetSummary()
        {
            var cores = GetCoreInfos();
            return new CpuSummary(
                GetProcessorName(),
                Environment.ProcessorCount,
                cores,
                GetActivePowerScheme());
        }

        public IReadOnlyList<ProcessCpuInfo> GetProcesses()
        {
            var processes = new List<ProcessCpuInfo>();
            foreach (var process in Process.GetProcesses().OrderBy(item => item.ProcessName))
            {
                using (process)
                {
                    try
                    {
                        processes.Add(new ProcessCpuInfo(
                            process.Id,
                            process.ProcessName,
                            process.MainWindowTitle,
                            process.PriorityClass.ToString(),
                            "0x" + process.ProcessorAffinity.ToInt64().ToString("X"),
                            process.TotalProcessorTime.ToString(@"hh\:mm\:ss"),
                            (process.WorkingSet64 / 1024D / 1024D).ToString("0.0") + " MB"));
                    }
                    catch
                    {
                        // Some system processes deny access. They are skipped to keep monitoring usable.
                    }
                }
            }

            return processes;
        }

        public OperationResult SetHighPerformancePowerPlan()
        {
            return RunProcess("powercfg", "/setactive SCHEME_MIN", "高性能电源计划已启用。");
        }

        public OperationResult ApplyLowLatencyPowerSettings()
        {
            var commands = new[]
            {
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100",
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100",
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL 1",
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100",
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMAXCORES 100",
                "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL 1",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMAXCORES 100",
                "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0"
            };

            var failures = new List<string>();
            foreach (var command in commands)
            {
                var result = RunProcess("powercfg", command, string.Empty);
                if (!result.Succeeded)
                {
                    failures.Add(command + ": " + result.Message);
                }
            }

            var activeResult = RunProcess("powercfg", "/setactive SCHEME_CURRENT", string.Empty);
            if (!activeResult.Succeeded)
            {
                failures.Add("/setactive SCHEME_CURRENT: " + activeResult.Message);
            }

            if (failures.Count > 0)
            {
                return OperationResult.Failure("部分 CPU 低延迟电源参数写入失败：\n" + string.Join(Environment.NewLine, failures));
            }

            return OperationResult.Success("CPU 低延迟电源参数已应用：最小/最大处理器状态 100%，主动散热，核心停车关闭，性能偏好最高。");
        }

        public OperationResult SetProcessHighPriority(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.PriorityClass = ProcessPriorityClass.High;
                }

                return OperationResult.Success("进程优先级已设置为 High。");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        public OperationResult RestrictProcessToPerformanceCores(int processId)
        {
            var cores = GetCoreInfos();
            if (cores.Select(item => item.EfficiencyClass).Distinct().Count() <= 1)
            {
                return OperationResult.Failure("未检测到可区分的大核/小核。该 CPU 可能不是混合架构，或当前系统未暴露 EfficiencyClass。");
            }

            var performanceCores = cores.Where(item => item.IsPerformanceCore).ToList();
            if (performanceCores.Count == 0)
            {
                return OperationResult.Failure("未检测到可区分的大核/小核。该 CPU 可能不是混合架构，或当前系统未暴露 EfficiencyClass。");
            }

            var mask = performanceCores.Aggregate(0L, (current, core) => current | core.AffinityMask);
            return SetProcessAffinity(processId, mask, "进程已限制到高性能核心。");
        }

        public OperationResult RestoreProcessToAllCores(int processId)
        {
            var mask = 0L;
            var count = Math.Min(Environment.ProcessorCount, 63);
            for (var index = 0; index < count; index++)
            {
                mask |= 1L << index;
            }

            return SetProcessAffinity(processId, mask, "进程已恢复为使用全部逻辑处理器。");
        }

        private static OperationResult SetProcessAffinity(int processId, long mask, string successMessage)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.ProcessorAffinity = new IntPtr(mask);
                }

                return OperationResult.Success(successMessage);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        private static IReadOnlyList<CpuCoreInfo> GetCoreInfos()
        {
            var cores = GetCoreInfosFromWindows();
            if (cores.Count == 0)
            {
                cores = new List<CpuCoreInfo>();
                for (var index = 0; index < Math.Min(Environment.ProcessorCount, 63); index++)
                {
                    cores.Add(new CpuCoreInfo(index, 0, true, 1L << index));
                }
            }

            var maxEfficiency = cores.Max(item => item.EfficiencyClass);
            return cores
                .Select(item => new CpuCoreInfo(item.LogicalProcessorIndex, item.EfficiencyClass, item.EfficiencyClass == maxEfficiency, item.AffinityMask))
                .OrderBy(item => item.LogicalProcessorIndex)
                .ToList();
        }

        private static List<CpuCoreInfo> GetCoreInfosFromWindows()
        {
            var result = new List<CpuCoreInfo>();
            var length = 0;
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
            if (length <= 0)
            {
                return result;
            }

            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
                {
                    return result;
                }

                var offset = 0;
                while (offset < length)
                {
                    var relationship = Marshal.ReadInt32(buffer, offset);
                    var size = Marshal.ReadInt32(buffer, offset + 4);
                    if (relationship == RelationProcessorCore && size >= 48)
                    {
                        var efficiencyClass = Marshal.ReadByte(buffer, offset + 9);
                        var groupCount = Marshal.ReadInt16(buffer, offset + 30);
                        var groupOffset = offset + 32;
                        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
                        {
                            var mask = Marshal.ReadInt64(buffer, groupOffset + groupIndex * 16);
                            AddLogicalProcessors(result, efficiencyClass, mask);
                        }
                    }

                    if (size <= 0)
                    {
                        break;
                    }

                    offset += size;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return result;
        }

        private static void AddLogicalProcessors(ICollection<CpuCoreInfo> result, byte efficiencyClass, long mask)
        {
            for (var index = 0; index < 63; index++)
            {
                var logicalMask = 1L << index;
                if ((mask & logicalMask) != 0)
                {
                    result.Add(new CpuCoreInfo(index, efficiencyClass, true, logicalMask));
                }
            }
        }

        private static string GetProcessorName()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
            {
                return key == null
                    ? "Unknown CPU"
                    : (key.GetValue("ProcessorNameString") as string ?? "Unknown CPU").Trim();
            }
        }

        private static string GetActivePowerScheme()
        {
            var result = RunProcess("powercfg", "/getactivescheme", string.Empty);
            return result.Succeeded ? result.Message.Trim() : result.Message;
        }

        private static OperationResult RunProcess(string fileName, string arguments, string successMessage)
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.Default,
                    StandardErrorEncoding = Encoding.Default
                }))
                {
                    if (process == null)
                    {
                        return OperationResult.Failure(fileName + " could not be started.");
                    }

                    process.WaitForExit(15000);
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    if (process.ExitCode == 0)
                    {
                        return OperationResult.Success(string.IsNullOrWhiteSpace(successMessage) ? output : successMessage);
                    }

                    return OperationResult.Failure(string.IsNullOrWhiteSpace(error) ? output : error);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref int returnedLength);
    }
}
