# Packaging Inspection Tools

Visual Studio desktop tool for packaging inspection deployment engineers.

The first version focuses on network adapter inspection and tuning for high-speed camera acquisition. CPU and GPU tuning modules can be added later through the same service boundary used by the network module.

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

Import and copy operations match settings by the driver property key. Unsupported properties or unsupported values on the target adapter are skipped.

## Permissions

Reading most values works as a normal user. Writing advanced NIC parameters and restarting adapters require running the tool as Administrator.

## Build

Open `PackagingInspectionTools.sln` in Visual Studio 2017 or later, or run:

```powershell
dotnet build PackagingInspectionTools.sln
```
