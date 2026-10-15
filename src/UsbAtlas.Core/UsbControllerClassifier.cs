namespace UsbAtlas.Core;

public sealed class UsbControllerClassifier
{
    // No historical vendor-wide/PCI-location heuristics. Mappings need a verified, versioned source.
    public string Classify(string? vendorId, string? deviceId) => "UNMAPPED";
}
