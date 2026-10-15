using UsbAtlas.Core;

namespace UsbAtlas.Tests;

public class ControllerCatalogTests
{
    private static readonly UsbControllerCatalog Catalog = new();

    [Fact]
    public void CatalogLoadsFromTheEmbeddedResource()
    {
        Assert.True(Catalog.Count > 250);
        Assert.Matches(@"^\d{4}\.\d{2}\.\d{2}$", Catalog.Version);
        Assert.Contains("pci.ids", Catalog.Source);
        Assert.StartsWith("https://", Catalog.Url);
    }

    [Theory]
    [InlineData("1022", "15B6", ControllerPlacement.CpuSoc)]
    [InlineData("1022", "149c", ControllerPlacement.CpuSoc)]
    [InlineData("1022", "1639", ControllerPlacement.CpuSoc)]
    [InlineData("1022", "43EE", ControllerPlacement.Chipset)]
    [InlineData("1022", "43FC", ControllerPlacement.Chipset)]
    [InlineData("1022", "43D5", ControllerPlacement.Chipset)]
    [InlineData("8086", "A2AF", ControllerPlacement.Chipset)]
    [InlineData("8086", "7A60", ControllerPlacement.Chipset)]
    [InlineData("8086", "7F6E", ControllerPlacement.Chipset)]
    [InlineData("8086", "9D2F", ControllerPlacement.OnPackageChipset)]
    [InlineData("8086", "A0ED", ControllerPlacement.OnPackageChipset)]
    [InlineData("8086", "7E7D", ControllerPlacement.OnPackageChipset)]
    [InlineData("8086", "9A13", ControllerPlacement.CpuSoc)]
    [InlineData("8086", "E37D", ControllerPlacement.OnPackageChipset)]
    [InlineData("8086", "E331", ControllerPlacement.CpuSoc)]
    [InlineData("8086", "7EB0", ControllerPlacement.CpuSoc)]
    [InlineData("1022", "162C", ControllerPlacement.CpuSoc)]
    [InlineData("1022", "15B9", ControllerPlacement.CpuSoc)]
    [InlineData("1B21", "1242", ControllerPlacement.DiscreteController)]
    [InlineData("1106", "3483", ControllerPlacement.DiscreteController)]
    [InlineData("1912", "0014", ControllerPlacement.DiscreteController)]
    [InlineData("8086", "1D26", ControllerPlacement.Chipset)]
    [InlineData("8086", "15D4", ControllerPlacement.DiscreteController)]
    [InlineData("8086", "15B6", ControllerPlacement.DiscreteController)]
    public void KnownControllersHaveTheExpectedPlacement(string vendor, string device, ControllerPlacement expected)
    {
        var entry = Catalog.Find(vendor, device);
        Assert.NotNull(entry);
        Assert.Equal(expected, entry.Placement);
        Assert.NotEmpty(entry.Name);
        Assert.Contains(entry.Confidence, new[] { "High", "Medium" });
        Assert.NotEmpty(entry.Source);
    }

    [Fact]
    public void SecondarySourceEntriesCarryMediumConfidence()
    {
        Assert.Equal("Medium", Catalog.Find("1022", "15B9")!.Confidence);
        Assert.Equal("Medium", Catalog.Find("1022", "43D0")!.Confidence);
        Assert.Equal("High", Catalog.Find("1022", "15B6")!.Confidence);
        Assert.StartsWith("pci.ids", Catalog.Find("1022", "15B6")!.Source);
    }

    [Theory]
    [InlineData("1022", "43F4")]
    [InlineData("8086", "0000")]
    [InlineData("1B21", "2425")]
    [InlineData(null, "15B6")]
    [InlineData("1022", "")]
    public void UnknownOrIncompleteIdsAreNotInTheCatalog(string? vendor, string device)
    {
        Assert.Null(Catalog.Find(vendor, device));
    }

    [Fact]
    public void NoEntryIsAUsbDeviceControllerOrRootPort()
    {
        var catalog = new UsbControllerCatalog();
        foreach (var id in new[] { ("8086", "7AE1"), ("8086", "7EC4"), ("8086", "A130"), ("1022", "13ED") })
            Assert.Null(catalog.Find(id.Item1, id.Item2));
    }
}
