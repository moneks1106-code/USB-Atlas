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

The controller placement catalog (`src/UsbAtlas.Core/controller-catalog.json`) is an
extracted subset of the PCI ID Repository's `pci.ids` (https://pci-ids.ucw.cz/, version
2026.10.15), limited to Intel and AMD USB host controllers. pci.ids is a compilation of
factual data maintained by Albert Pool, Martin Mares and volunteers; it can be distributed
under the GNU GPL v2 or later or the 3-clause BSD License. USB Atlas redistributes the subset
under the 3-clause BSD License; see `docs/licenses/PCI-IDS-LICENSE.txt`. The placement class
of each entry (CPU/SoC, chipset, on-package chipset, add-in controller) is USB Atlas's own
inference from the product name, produced by `tools/generate-controller-catalog.py`.

Development dependencies include xUnit 2.9.3, xUnit VS Runner 4.0.1,
Microsoft.NET.Test.Sdk 18.11.0 and Windows SDK BuildTools.
