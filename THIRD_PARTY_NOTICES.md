# Dependencies and references

USB Atlas uses Microsoft .NET 10, Windows App SDK 2.5.1 (including WinUI 3) and
Microsoft.Extensions.DependencyInjection 10.0.13. Runtime components are distributed
under their respective Microsoft/NuGet redistribution terms. License/notice copies are
included under `docs/licenses` in source and `licenses` in the portable release.

- .NET: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- .NET notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- Windows App SDK: https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE
- Windows App SDK package: https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1
- Dependency Injection: https://github.com/dotnet/runtime/tree/main/src/libraries/Microsoft.Extensions.DependencyInjection

Microsoft USBView is a public API and enumeration-flow reference. No USBView or USBTreeView
program or source is bundled. The public evidence document summarizes implementation
contracts and limitations; private authoring research and development prompts are not included.
Historical research references have not all been independently reverified.

The UI model-hint catalog uses individual PCI model facts from PCI ID Project's pci.ids
version 2026.10.15 (https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids) and
Linux kernel PROM21 xHCI documentation
(https://www.kernel.org/doc/html/latest/hwmon/prom21-xhci.html, checked 2026-10-15).
Neither the database nor Linux kernel source code is distributed.

Development dependencies include xUnit 2.9.3, xUnit VS Runner 4.0.1,
Microsoft.NET.Test.Sdk 18.11.0 and Windows SDK BuildTools.
