using UsbAtlas.Core;
namespace UsbAtlas.Tests;

public class DescriptorTests
{
    [Fact] public void CompositeMouseAndKeyboardIsOnePhysicalRoleWithoutInventedRadio()
    { Assert.Equal(DeviceRole.Other, new UsbDeviceNormalizer().Classify(0, [3], ["Mouse", "Keyboard", "HIDClass"])); }
    [Theory] [InlineData(1, 125)] [InlineData(4, 1000)] [InlineData(16, 4096000)]
    public void HighSpeedIntervalsAreExponential(byte interval, double expected)
    { Assert.Equal(expected, new Endpoint(0x81, 3, 64, interval).ServiceIntervalUs(LinkSpeed.High)); }
    [Fact] public void FullSpeedIsNotHighSpeed()
    { Assert.Equal(1000, new Endpoint(0x81, 3, 64, 1).ServiceIntervalUs(LinkSpeed.Full)); }
    [Fact] public void MalformedDescriptorFailsClosed()
    { Assert.Throws<InvalidDataException>(() => UsbDeviceNormalizer.ParseConfiguration([9, 2, 11, 0, 1, 1, 0, 128, 50, 0, 5])); }
    [Fact] public void OfferedAlternateSettingsRemainDistinct()
    {
        byte[] config = [9,2,41,0,1,1,0,128,50, 9,4,0,0,1,14,1,0,0, 7,5,0x81,2,0,2,0, 9,4,0,1,1,14,1,0,0, 7,5,0x81,1,0,2,1];
        var parsed = UsbDeviceNormalizer.ParseConfiguration(config);
        Assert.Equal(2, parsed.Endpoints.Length);
        Assert.Equal((byte)0, parsed.Endpoints[0].AlternateSetting);
        Assert.Equal((byte)1, parsed.Endpoints[1].AlternateSetting);
    }
    [Fact] public void HistoricalPciIdsAreNotSilentlyTrusted()
    { Assert.Equal("UNMAPPED", new UsbControllerClassifier().Classify("1022", "43F4")); }
}
