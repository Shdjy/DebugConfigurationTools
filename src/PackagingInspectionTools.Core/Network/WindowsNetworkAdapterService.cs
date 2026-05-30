using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;

namespace PackagingInspectionTools.Core.Network
{
    public sealed class WindowsNetworkAdapterService : INetworkAdapterService
    {
        private const string NetworkAdapterClassKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

        public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
        {
            var registryAdapters = LoadRegistryAdapters();
            var adapters = new List<NetworkAdapterInfo>();

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                registryAdapters.TryGetValue(networkInterface.Id, out var registryPath);

                adapters.Add(new NetworkAdapterInfo(
                    networkInterface.Id,
                    networkInterface.Name,
                    networkInterface.Description,
                    registryPath,
                    networkInterface.OperationalStatus.ToString(),
                    networkInterface.NetworkInterfaceType.ToString(),
                    FormatMacAddress(networkInterface.GetPhysicalAddress()),
                    GetPrimaryIPv4Address(networkInterface),
                    GetPrimaryIPv4SubnetMask(networkInterface),
                    registryPath == null ? string.Empty : GetRegistryValue(registryPath, "DriverVersion"),
                    networkInterface.Speed,
                    registryPath == null ? Array.Empty<AdapterAdvancedProperty>() : ReadAdvancedProperties(registryPath)));
            }

            return adapters
                .OrderByDescending(adapter => adapter.OperationalStatus == "Up")
                .ThenBy(adapter => adapter.Name)
                .ToList();
        }

        public OperationResult SetAdvancedProperty(NetworkSettingUpdate update)
        {
            var adapter = GetAdapters().FirstOrDefault(item => item.Id == update.AdapterId);
            if (adapter == null || string.IsNullOrWhiteSpace(adapter.RegistryPath))
            {
                return OperationResult.Failure("Adapter registry path was not found.");
            }

            var property = adapter.AdvancedProperties.FirstOrDefault(item => item.Key == update.PropertyKey);
            if (property == null)
            {
                return OperationResult.Failure("Selected property was not found.");
            }

            if (!property.IsWritable)
            {
                return OperationResult.Failure("Selected property is read-only.");
            }

            if (property.Options.Count > 0 && property.Options.All(item => item.Value != update.Value))
            {
                return OperationResult.Failure("Selected value is not supported by this adapter driver.");
            }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(adapter.RegistryPath, writable: true))
                {
                    if (key == null)
                    {
                        return OperationResult.Failure("Adapter registry key could not be opened.");
                    }

                    key.SetValue(update.PropertyKey, update.Value, RegistryValueKind.String);
                }

                return OperationResult.Success("Setting was written. Restart the adapter or reboot Windows to apply it.");
            }
            catch (UnauthorizedAccessException)
            {
                return OperationResult.Failure("Administrator permission is required to change adapter settings.");
            }
            catch (SecurityException)
            {
                return OperationResult.Failure("Administrator permission is required to change adapter settings.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        public OperationResult ApplyGigECameraPreset(string adapterId)
        {
            var adapter = GetAdapters().FirstOrDefault(item => item.Id == adapterId);
            if (adapter == null)
            {
                return OperationResult.Failure("Adapter was not found.");
            }

            var updates = new List<NetworkSettingUpdate>();
            AddBestOption(adapter, updates, new[] { "jumbo", "jumbopacket" }, new[] { "9014", "9000", "9000 bytes", "9kb", "jumbo" });
            AddBestOption(adapter, updates, new[] { "speed", "duplex" }, new[] { "1.0 gbps full duplex", "1 gbps full duplex", "1000 mbps full duplex", "auto negotiation", "auto" });
            AddBestOption(adapter, updates, new[] { "interrupt", "moderation" }, new[] { "disabled", "off", "0" });
            AddBestOption(adapter, updates, new[] { "receive", "buffers" }, new[] { "4096", "2048", "1024" });
            AddBestOption(adapter, updates, new[] { "transmit", "buffers" }, new[] { "4096", "2048", "1024" });
            AddBestOption(adapter, updates, new[] { "energy", "efficient" }, new[] { "disabled", "off", "0" });

            if (updates.Count == 0)
            {
                return OperationResult.Failure("No matching GigE camera settings were exposed by this adapter driver.");
            }

            var failures = new List<string>();
            foreach (var update in updates)
            {
                var result = SetAdvancedProperty(update);
                if (!result.Succeeded)
                {
                    failures.Add($"{update.PropertyKey}: {result.Message}");
                }
            }

            return failures.Count == 0
                ? OperationResult.Success($"Applied {updates.Count} matching GigE camera settings. Restart the adapter to apply them.")
                : OperationResult.Failure(string.Join(Environment.NewLine, failures));
        }

        public OperationResult RestartAdapter(string adapterName)
        {
            var disable = RunNetsh($"interface set interface name=\"{adapterName}\" admin=disabled");
            if (!disable.Succeeded)
            {
                return disable;
            }

            var enable = RunNetsh($"interface set interface name=\"{adapterName}\" admin=enabled");
            return enable.Succeeded
                ? OperationResult.Success("Adapter restart command completed.")
                : enable;
        }

        public OperationResult SetStaticIPv4Address(string adapterName, string ipAddress, string subnetMask)
        {
            return RunNetsh($"interface ipv4 set address name=\"{adapterName}\" source=static address={ipAddress} mask={subnetMask} gateway=none");
        }

        public OperationResult EnableDhcpIPv4(string adapterName)
        {
            return RunNetsh($"interface ipv4 set address name=\"{adapterName}\" source=dhcp");
        }

        public OperationResult Ping(NetworkPingRequest request)
        {
            var arguments = BuildPingArguments(request);
            return RunProcess("ping", arguments);
        }

        public OperationResult Ping(NetworkPingRequest request, Action<string> outputReceived)
        {
            var arguments = BuildPingArguments(request);
            return RunProcessRealtime("ping", arguments, outputReceived);
        }

        private static Dictionary<string, string> LoadRegistryAdapters()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var classKey = Registry.LocalMachine.OpenSubKey(NetworkAdapterClassKey))
            {
                if (classKey == null)
                {
                    return result;
                }

                foreach (var subKeyName in classKey.GetSubKeyNames().Where(name => name.All(char.IsDigit)))
                {
                    using (var adapterKey = classKey.OpenSubKey(subKeyName))
                    {
                        var id = adapterKey?.GetValue("NetCfgInstanceId") as string;
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            result[id] = NetworkAdapterClassKey + "\\" + subKeyName;
                        }
                    }
                }
            }

            return result;
        }

        private static IReadOnlyList<AdapterAdvancedProperty> ReadAdvancedProperties(string registryPath)
        {
            var properties = new List<AdapterAdvancedProperty>();
            using (var adapterKey = Registry.LocalMachine.OpenSubKey(registryPath))
            using (var parametersKey = Registry.LocalMachine.OpenSubKey(registryPath + @"\Ndi\Params"))
            {
                if (adapterKey == null || parametersKey == null)
                {
                    return properties;
                }

                foreach (var propertyKey in parametersKey.GetSubKeyNames())
                {
                    using (var parameterKey = parametersKey.OpenSubKey(propertyKey))
                    {
                        if (parameterKey == null)
                        {
                            continue;
                        }

                        var displayName = parameterKey.GetValue("ParamDesc") as string ?? propertyKey;
                        var currentValue = adapterKey.GetValue(propertyKey)?.ToString();
                        var defaultValue = parameterKey.GetValue("Default")?.ToString();
                        var options = ReadOptions(parameterKey);

                        properties.Add(new AdapterAdvancedProperty(
                            propertyKey,
                            displayName,
                            currentValue ?? defaultValue,
                            options,
                            true));
                    }
                }
            }

            return properties.OrderBy(item => item.DisplayName).ToList();
        }

        private static IReadOnlyList<NetworkSettingOption> ReadOptions(RegistryKey parameterKey)
        {
            using (var enumKey = parameterKey.OpenSubKey("enum"))
            {
                if (enumKey == null)
                {
                    return Array.Empty<NetworkSettingOption>();
                }

                return enumKey
                    .GetValueNames()
                    .OrderBy(name => name)
                    .Select(name => new NetworkSettingOption(name, enumKey.GetValue(name)?.ToString() ?? name))
                    .ToList();
            }
        }

        private static void AddBestOption(
            NetworkAdapterInfo adapter,
            ICollection<NetworkSettingUpdate> updates,
            IReadOnlyList<string> propertyWords,
            IReadOnlyList<string> preferredValues)
        {
            var property = adapter.AdvancedProperties.FirstOrDefault(item =>
                propertyWords.All(word => Normalize(item.Key + " " + item.DisplayName).Contains(word)));

            if (property == null)
            {
                return;
            }

            var option = property.Options.FirstOrDefault(item =>
                preferredValues.Any(value => Normalize(item.Value + " " + item.DisplayName).Contains(Normalize(value))));

            if (option != null)
            {
                updates.Add(new NetworkSettingUpdate(adapter.Id, property.Key, option.Value));
            }
        }

        private static OperationResult RunNetsh(string arguments)
        {
            return RunProcess("netsh", arguments);
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
                    RedirectStandardOutput = true
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

        private static OperationResult RunProcessRealtime(string fileName, string arguments, Action<string> outputReceived)
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
                    StandardOutputEncoding = System.Text.Encoding.Default,
                    StandardErrorEncoding = System.Text.Encoding.Default
                }))
                {
                    if (process == null)
                    {
                        return OperationResult.Failure(fileName + " could not be started.");
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        outputReceived(process.StandardOutput.ReadLine() + Environment.NewLine);
                    }

                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(15000);

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        outputReceived(error + Environment.NewLine);
                    }

                    return process.ExitCode == 0
                        ? OperationResult.Success("Ping completed.")
                        : OperationResult.Failure(string.IsNullOrWhiteSpace(error) ? "Ping failed." : error);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(ex.Message);
            }
        }

        private static string Normalize(string value)
        {
            return value.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        private static string BuildPingArguments(NetworkPingRequest request)
        {
            var arguments = new List<string>
            {
                "-n",
                Clamp(request.Count, 1, 100).ToString(),
                "-w",
                Clamp(request.TimeoutMilliseconds, 100, 60000).ToString(),
                "-l",
                Clamp(request.BufferSize, 0, 65500).ToString(),
                "-i",
                Clamp(request.Ttl, 1, 255).ToString()
            };

            if (request.DontFragment)
            {
                arguments.Add("-f");
            }

            if (!string.IsNullOrWhiteSpace(request.SourceAddress))
            {
                arguments.Add("-S");
                arguments.Add(request.SourceAddress);
            }

            arguments.Add(request.Target);
            return string.Join(" ", arguments.Select(QuoteArgument));
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", string.Empty) + "\"";
        }

        private static string FormatMacAddress(PhysicalAddress physicalAddress)
        {
            var bytes = physicalAddress.GetAddressBytes();
            return bytes.Length == 0 ? null : string.Join(":", bytes.Select(item => item.ToString("X2")));
        }

        private static string GetPrimaryIPv4Address(NetworkInterface networkInterface)
        {
            var address = networkInterface.GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(item => item.Address.AddressFamily == AddressFamily.InterNetwork);
            return address == null ? string.Empty : address.Address.ToString();
        }

        private static string GetPrimaryIPv4SubnetMask(NetworkInterface networkInterface)
        {
            var address = networkInterface.GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(item => item.Address.AddressFamily == AddressFamily.InterNetwork);
            return address == null || address.IPv4Mask == null ? string.Empty : address.IPv4Mask.ToString();
        }

        private static string GetRegistryValue(string registryPath, string valueName)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                return key == null ? string.Empty : key.GetValue(valueName)?.ToString() ?? string.Empty;
            }
        }
    }
}
