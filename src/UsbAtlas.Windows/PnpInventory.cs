using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static UsbAtlas.Windows.NativeMethods;

namespace UsbAtlas.Windows;

public sealed record PnpDevice(string InstanceId, string? ParentId, string? ContainerId,
    string Name, string? Manufacturer, string? DriverKey, string? LocationPath, string Class, string[] HardwareIds);

internal sealed class PnpInventory
{
    public PnpDevice[] Read()
    {
        var set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, 6);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate Windows device information.");
        try
        {
            var devices = new List<PnpDevice>();
            for (uint i = 0; ; i++)
            {
                var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, i, ref info))
                { if (Marshal.GetLastWin32Error() == 259) break; throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate Windows device information."); }
                var id = new StringBuilder(512);
                if (!SetupDiGetDeviceInstanceIdW(set, ref info, id, 512, out _)) continue;
                var parent = new StringBuilder(512);
                string? parentId = CM_Get_Parent(out var parentInst, info.DevInst, 0) == 0 && CM_Get_Device_IDW(parentInst, parent, 512, 0) == 0 ? parent.ToString() : null;
                var key = new PropertyKey { Format = ContainerFormat, Id = 2 };
                var buffer = new byte[16];
                string? container = SetupDiGetDevicePropertyW(set, ref info, ref key, out _, buffer, 16, out var required, 0) && required == 16
                    ? new Guid(buffer).ToString() : null;
                devices.Add(new(id.ToString(), parentId, container,
                    Property(set, ref info, 12) ?? Property(set, ref info, 0) ?? id.ToString(),
                    Property(set, ref info, 11), Property(set, ref info, 9), Property(set, ref info, 35),
                    Property(set, ref info, 7) ?? "", (Property(set, ref info, 1) ?? "").Split('\0', StringSplitOptions.RemoveEmptyEntries)));
            }
            return devices.ToArray();
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    public (string Path, string InstanceId)[] Interfaces(Guid guid)
    {
        var guidPtr = Marshal.AllocHGlobal(16);
        Marshal.StructureToPtr(guid, guidPtr, false);
        var set = SetupDiGetClassDevsW(guidPtr, null, IntPtr.Zero, 18);
        Marshal.FreeHGlobal(guidPtr);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate Windows device information.");
        try
        {
            var result = new List<(string, string)>();
            for (uint i = 0; ; i++)
            {
                var item = new InterfaceInfo { Size = (uint)Marshal.SizeOf<InterfaceInfo>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref item))
                { if (Marshal.GetLastWin32Error() == 259) break; throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate Windows device information."); }
                var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                SetupDiGetDeviceInterfaceDetailW(set, ref item, IntPtr.Zero, 0, out var size, ref info);
                if (size < 8 || size > 65536) continue;
                var detail = Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(set, ref item, detail, size, out _, ref info)) continue;
                    var id = new StringBuilder(512);
                    if (!SetupDiGetDeviceInstanceIdW(set, ref info, id, 512, out _)) continue;
                    result.Add((Marshal.PtrToStringUni(detail + 4)!, id.ToString()));
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            return result.ToArray();
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    private static string? Property(IntPtr set, ref DeviceInfo info, uint property)
    {
        var buffer = new byte[8192];
        return SetupDiGetDeviceRegistryPropertyW(set, ref info, property, out _, buffer, (uint)buffer.Length, out var length)
            ? Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(length, buffer.Length)).TrimEnd('\0') : null;
    }
}
