namespace UsbAtlas.Core;

public sealed class UsbDeviceNormalizer
{
    public DeviceRole[] Functions(IEnumerable<string> childClasses)
    {
        var classes = childClasses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = new List<DeviceRole>();
        if (classes.Contains("Mouse")) roles.Add(DeviceRole.Mouse);
        if (classes.Contains("Keyboard")) roles.Add(DeviceRole.Keyboard);
        return roles.ToArray();
    }
    public DeviceRole Classify(byte? deviceClass, byte[] interfaces, IEnumerable<string> childClasses)
    {
        var children = childClasses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (deviceClass == 9) return DeviceRole.Hub;
        // Composite receiver with both functions stays one physical device, with no invented RF transport.
        if (children.Contains("Mouse") && children.Contains("Keyboard")) return DeviceRole.Other;
        if (children.Contains("Mouse")) return DeviceRole.Mouse;
        if (children.Contains("Keyboard")) return DeviceRole.Keyboard;
        if (deviceClass == 14 || interfaces.Contains((byte)14)) return DeviceRole.Camera;
        if (deviceClass == 1 || interfaces.Contains((byte)1)) return DeviceRole.Audio;
        if (deviceClass == 8 || interfaces.Contains((byte)8)) return DeviceRole.Storage;
        return DeviceRole.Unknown;
    }

    public static (Endpoint[] Endpoints, byte[] Classes) ParseConfiguration(byte[] data)
    {
        var endpoints = new List<Endpoint>();
        var classes = new HashSet<byte>();
        byte? number = null, alt = null;
        if (data.Length < 9 || data[0] < 9 || data[1] != 2) throw new InvalidDataException("Invalid configuration descriptor.");
        var total = data[2] | data[3] << 8;
        if (total > data.Length || total < 9) throw new InvalidDataException("Incomplete configuration descriptor.");
        for (int offset = 0; offset < total;)
        {
            int length = data[offset];
            if (length < 2 || offset + length > total) throw new InvalidDataException("Invalid descriptor length.");
            if (data[offset + 1] == 4 && length >= 9)
            { number = data[offset + 2]; alt = data[offset + 3]; classes.Add(data[offset + 5]); }
            if (data[offset + 1] == 5 && length >= 7)
                endpoints.Add(new(data[offset + 2], (byte)(data[offset + 3] & 3),
                    (ushort)(data[offset + 4] | data[offset + 5] << 8), data[offset + 6], number, alt));
            offset += length;
        }
        return (endpoints.ToArray(), classes.ToArray());
    }
}
