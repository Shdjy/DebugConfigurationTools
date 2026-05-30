using PackagingInspectionTools.Core.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;

namespace PackagingInspectionTools.Core.Gpu
{
    public sealed class WindowsGpuService : IGpuService
    {
        public IReadOnlyList<GpuInfo> GetGpus()
        {
            if (IsNvidiaSmiAvailable())
            {
                var nvidia = GetNvidiaGpus();
                if (nvidia.Count > 0)
                {
                    return nvidia;
                }
            }

            return GetWmiGpus();
        }

        public OperationResult EnablePersistenceMode(string gpuIndex)
        {
            return RunNvidiaSmi("-i " + Quote(gpuIndex) + " -pm 1", "NVIDIA Persistence Mode 已启用。");
        }

        public OperationResult SetPowerLimit(string gpuIndex, int watts)
        {
            if (watts <= 0)
            {
                return OperationResult.Failure("功耗上限必须大于 0 W。");
            }

            return RunNvidiaSmi("-i " + Quote(gpuIndex) + " -pl " + watts, "GPU 功耗上限已设置。");
        }

        public OperationResult LockGraphicsClock(string gpuIndex, int minMhz, int maxMhz)
        {
            if (minMhz <= 0 || maxMhz <= 0 || minMhz > maxMhz)
            {
                return OperationResult.Failure("核心频率范围不合法。");
            }

            return RunNvidiaSmi("-i " + Quote(gpuIndex) + " -lgc " + minMhz + "," + maxMhz, "GPU 核心频率已锁定。");
        }

        public OperationResult ResetGraphicsClock(string gpuIndex)
        {
            return RunNvidiaSmi("-i " + Quote(gpuIndex) + " -rgc", "GPU 核心频率锁定已恢复默认。");
        }

        public OperationResult SetComputeMode(string gpuIndex, string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return OperationResult.Failure("请选择计算模式。");
            }

            return RunNvidiaSmi("-i " + Quote(gpuIndex) + " -c " + Quote(mode), "GPU 计算模式已设置。");
        }

        public OperationResult SetDriverModel(string gpuIndex, bool tccMode)
        {
            return RunNvidiaSmi("-g " + Quote(gpuIndex) + " -dm " + (tccMode ? "1" : "0"), tccMode ? "GPU 驱动模式已请求切换为 TCC。该设置可能需要重启后生效。" : "GPU 驱动模式已请求切换为 WDDM。该设置可能需要重启后生效。");
        }

        public OperationResult DisablePcieLinkStatePowerManagement()
        {
            var failures = new List<string>();
            var commands = new[]
            {
                "/setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0",
                "/setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0",
                "/setactive SCHEME_CURRENT"
            };

            foreach (var command in commands)
            {
                var result = RunProcess("powercfg", command);
                if (!result.Succeeded)
                {
                    failures.Add(command + ": " + result.Message);
                }
            }

            return failures.Count == 0
                ? OperationResult.Success("PCIe 链路状态电源管理已关闭。")
                : OperationResult.Failure("PCIe 链路状态电源管理设置失败：\n" + string.Join(Environment.NewLine, failures));
        }

        private static IReadOnlyList<GpuInfo> GetNvidiaGpus()
        {
            const string query = "--query-gpu=index,name,driver_version,utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw,power.limit,clocks.gr,clocks.mem,compute_mode --format=csv,noheader,nounits";
            var result = RunProcess("nvidia-smi", query);
            if (!result.Succeeded)
            {
                return new List<GpuInfo>();
            }

            var gpus = new List<GpuInfo>();
            foreach (var line in result.Message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split(',').Select(item => item.Trim()).ToList();
                if (fields.Count < 12)
                {
                    continue;
                }

                gpus.Add(new GpuInfo(
                    fields[0],
                    fields[1],
                    fields[2],
                    fields[3] + " %",
                    fields[4] + " MB",
                    fields[5] + " MB",
                    fields[6] + " C",
                    fields[7] + " W",
                    fields[8] + " W",
                    fields[9] + " MHz",
                    fields[10] + " MHz",
                    fields[11],
                    "nvidia-smi"));
            }

            return gpus;
        }

        private static IReadOnlyList<GpuInfo> GetWmiGpus()
        {
            var result = new List<GpuInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController"))
                {
                    var index = 0;
                    foreach (ManagementObject item in searcher.Get())
                    {
                        using (item)
                        {
                            result.Add(new GpuInfo(
                                index.ToString(),
                                Convert.ToString(item["Name"]) ?? string.Empty,
                                Convert.ToString(item["DriverVersion"]) ?? string.Empty,
                                string.Empty,
                                string.Empty,
                                FormatBytes(item["AdapterRAM"]),
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                "WMI"));
                            index++;
                        }
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static OperationResult RunNvidiaSmi(string arguments, string successMessage)
        {
            if (!IsNvidiaSmiAvailable())
            {
                return OperationResult.Failure("未找到 nvidia-smi。GPU 设置功能目前仅支持已安装 NVIDIA 驱动并可访问 nvidia-smi 的环境。");
            }

            var result = RunProcess("nvidia-smi", arguments);
            return result.Succeeded ? OperationResult.Success(successMessage) : result;
        }

        private static bool IsNvidiaSmiAvailable()
        {
            return RunProcess("nvidia-smi", "--help").Succeeded;
        }

        private static OperationResult RunProcess(string fileName, string arguments)
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
                    return process.ExitCode == 0
                        ? OperationResult.Success(output)
                        : OperationResult.Failure(string.IsNullOrWhiteSpace(error) ? output : error);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        private static string FormatBytes(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            ulong bytes;
            return ulong.TryParse(value.ToString(), out bytes)
                ? (bytes / 1024D / 1024D).ToString("0") + " MB"
                : value.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", string.Empty) + "\"";
        }
    }
}
