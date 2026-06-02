# 调试配置检查工具

Visual Studio desktop tool for commissioning-stage configuration inspection and parameter tuning.

The tool focuses on commissioning-stage configuration checks and parameter tuning, including network adapter inspection/tuning for high-speed camera acquisition, CPU tuning for low-latency algorithm and Modbus communication workloads, and GPU monitoring/tuning for AI inference workloads.

## Projects

- `PackagingInspectionTools.Core`: business logic and Windows network adapter configuration.
- `PackagingInspectionTools.UI`: WinForms desktop interface for engineers.

## Current Network Features

- List physical and virtual network adapters visible to Windows.
- Read adapter link status, speed, MAC address, description, and registry-backed advanced driver parameters.
- Edit advanced driver parameters such as jumbo packet and speed/duplex when the NIC driver exposes them.
- Apply a GigE camera preset by selecting likely high-throughput values when those properties exist.
- Restart a selected adapter through `netsh` so driver settings can take effect.
- Export the selected adapter configuration and advanced driver parameters to a UTF-8 CSV file.
- Import an exported CSV configuration into the selected adapter.
- Copy the selected adapter's current driver parameters directly to another adapter.
- Compare the selected adapter against an exported standard CSV configuration.
- Disable DHCP on the selected adapter and set a manual IPv4 address and subnet mask.
- Restore the selected adapter to automatic IPv4 address acquisition.
- Ping another device with configurable count, timeout, packet size, TTL, and "don't fragment". When the selected adapter has an IPv4 address, it is used as the ping source address.

Import and copy operations match settings by the driver property key. Unsupported properties or unsupported values on the target adapter are skipped. Exported standard configurations include IPv4 address and subnet mask so compare results can show network address differences; IP address writes are still applied through the dedicated static IPv4 controls to avoid accidental duplicate addresses.

## Current CPU Features

- Monitor CPU model, logical processors, detected core efficiency class, and current Windows power scheme.
- Monitor running processes with priority, CPU affinity, CPU time, and memory usage.
- Enable the Windows high performance power plan.
- Apply low-latency CPU power settings for algorithm and communication workloads:
  - Processor minimum and maximum state: 100%.
  - Active cooling.
  - Core parking minimum and maximum cores: 100%.
  - Processor energy performance preference: highest performance, when supported by the OS.
- Set a selected algorithm or Modbus communication process to High priority.
- Restrict a selected process to high-performance cores only. This is the tool's "disable small cores" action and is implemented through process CPU affinity, not BIOS-level global E-core disabling.

## Current GPU Features

- Monitor GPU name, driver version, utilization, memory usage, temperature, power, clocks, and compute mode when NVIDIA `nvidia-smi` is available.
- Fall back to WMI display adapter information when `nvidia-smi` is unavailable.
- Enable NVIDIA persistence mode.
- Set NVIDIA GPU power limit.
- Lock NVIDIA graphics clock range to reduce inference latency jitter.
- Reset NVIDIA graphics clock lock.
- Set NVIDIA compute mode.
- Request NVIDIA TCC/WDDM driver model switching when supported by the GPU and driver.
- Disable PCI Express link state power management in the current Windows power plan.

GPU setting operations currently depend on NVIDIA `nvidia-smi` and usually require Administrator permission. Non-NVIDIA adapters are monitored through WMI only. TCC mode is only supported by specific NVIDIA professional/compute GPUs and generally requires another GPU or iGPU for Windows display output. PCIe ASPM changes are applied through Windows `powercfg`; BIOS-level ASPM settings may still need to be checked manually on deployment machines.

## Permissions

Reading most values works as a normal user. Writing advanced NIC parameters and restarting adapters require running the tool as Administrator.

## Build

Open `PackagingInspectionTools.sln` in Visual Studio 2017 or later, or run:

```powershell
dotnet build PackagingInspectionTools.sln
```

