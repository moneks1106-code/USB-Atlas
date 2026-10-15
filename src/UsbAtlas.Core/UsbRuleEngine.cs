namespace UsbAtlas.Core;

/// <summary>
/// Checks the observable properties of a snapshot. Only descriptor and enumeration facts are used;
/// nothing here measures traffic, report rates or latency.
/// </summary>
public sealed class UsbRuleEngine
{
    public const string HidServiceInterval = "HID_SERVICE_INTERVAL";
    public const string SuperSpeedLink = "SUPERSPEED_LINK";
    public const string PortError = "PORT_ERROR";

    private const string MsEndpoint = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor";
    private const string MsConnectionV2 = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_node_connection_information_ex_v2_flags";
    private const string MsUsbView = "https://github.com/microsoft/Windows-driver-samples/tree/main/usb/usbview";

    public Finding[] Evaluate(UsbSnapshot snapshot)
    {
        if (!snapshot.Consistent) return [];
        var findings = new List<Finding>();
        foreach (var device in snapshot.Devices)
        {
            if (ServiceInterval(device) is { } interval) findings.Add(interval);
            if (SuperSpeed(device) is { } link) findings.Add(link);
        }
        foreach (var port in snapshot.Ports.Where(p => p.State == PortState.Error))
        {
            findings.Add(new(PortError, port.Id, Assessment.Problem, "Windows reports a connection error",
                $"The hub reports USB_CONNECTION_STATUS={port.ConnectionStatus} for this port. The device on it did not enumerate correctly.",
                [$"Status={port.ConnectionStatus}"], [MsUsbView],
                "The status code does not establish its cause. Try another cable or port and refresh."));
        }
        return findings.ToArray();
    }

    private static Finding? ServiceInterval(UsbNode device)
    {
        if (!device.IsHid) return null;
        var pipe = device.OpenPipes.Where(p => p.IsInterruptIn)
            .Select(p => (Pipe: p, Interval: p.ServiceIntervalUs(device.Speed)))
            .Where(p => p.Interval is > 0)
            .OrderBy(p => p.Interval)
            .FirstOrDefault();
        if (pipe.Pipe == null) return null;
        double us = pipe.Interval!.Value;
        string rate = us < 1000 ? $"{us:0} µs (up to {1_000_000 / us:0} reports per second)" : $"{us / 1000:0.#} ms (up to {1_000_000 / us:0} reports per second)";
        return new(HidServiceInterval, device.Id, Assessment.Ok, "Offered interrupt service interval",
            $"The open interrupt-IN endpoint 0x{pipe.Pipe.Address:X2} offers a service interval of {rate} on the current {Speed(device.Speed)} link.",
            [$"Speed={device.Speed}", $"bInterval={pipe.Pipe.Interval}", $"Endpoint=0x{pipe.Pipe.Address:X2}"], [MsEndpoint],
            "This is the ceiling the descriptor offers. The actual report rate depends on the device and was not measured.");
    }

    private static Finding? SuperSpeed(UsbNode device)
    {
        if (device.SuperSpeedCapable != true || device.Speed == LinkSpeed.Unknown) return null;
        if (device.Speed >= LinkSpeed.Super)
            return new(SuperSpeedLink, device.Id, Assessment.Ok, "SuperSpeed link in use",
                "The device supports SuperSpeed and is connected with a SuperSpeed link.",
                [$"Speed={device.Speed}", "SuperSpeed capable"], [MsConnectionV2],
                "Link type alone does not establish throughput or latency.");
        return new(SuperSpeedLink, device.Id, Assessment.Note, "SuperSpeed-capable device on a slower link",
            $"The device supports SuperSpeed but is connected at {Speed(device.Speed)}. If you expect SuperSpeed, check the cable and the port.",
            [$"Speed={device.Speed}", "SuperSpeed capable"], [MsConnectionV2],
            "Some devices use a USB 2 link by design. Whether SuperSpeed is required was not established.");
    }

    private static string Speed(LinkSpeed speed) => speed switch
    {
        LinkSpeed.Low => "Low-Speed (1.5 Mbit/s)",
        LinkSpeed.Full => "Full-Speed (12 Mbit/s)",
        LinkSpeed.High => "High-Speed (480 Mbit/s)",
        LinkSpeed.Super => "SuperSpeed",
        LinkSpeed.SuperPlus => "SuperSpeedPlus",
        _ => "unknown speed"
    };
}
