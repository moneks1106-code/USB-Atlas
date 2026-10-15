using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UsbAtlas.Windows;

internal static class NativeMethods
{
    internal static readonly Guid HostControllerGuid = new("3abf6f2d-71c4-462a-8a92-1e6861e6af27");
    internal static readonly Guid HubGuid = new("f18a0e88-c30c-11d0-8815-00a0c906bed8");
    internal static readonly Guid ContainerFormat = new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c");
    [StructLayout(LayoutKind.Sequential)] internal struct DeviceInfo
    { internal uint Size; internal Guid ClassGuid; internal uint DevInst; internal IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] internal struct InterfaceInfo
    { internal uint Size; internal Guid ClassGuid; internal uint Flags; internal IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] internal struct PropertyKey
    { internal Guid Format; internal uint Id; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr window, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo data);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceInfo data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref InterfaceInfo data, IntPtr detail, uint size, out uint required, ref DeviceInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref DeviceInfo data, StringBuilder id, uint size, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref DeviceInfo data, uint property, out uint type, byte[] buffer, uint size, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref DeviceInfo data, ref PropertyKey key, out uint type, byte[] buffer, uint size, out uint required, uint flags);
    [DllImport("setupapi.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll")] internal static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] internal static extern uint CM_Get_Device_IDW(uint devInst, StringBuilder buffer, uint size, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFileW(string path, uint access, uint sharing, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(SafeFileHandle file, uint code, byte[] input, uint inputSize, byte[] output, uint outputSize, out uint returned, IntPtr overlapped);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate uint NotificationCallback(IntPtr notify, IntPtr context, uint action, IntPtr data, uint size);
    [DllImport("cfgmgr32.dll")] internal static extern uint CM_Register_Notification(byte[] filter, IntPtr context, NotificationCallback callback, out IntPtr handle);
    [DllImport("cfgmgr32.dll")] internal static extern uint CM_Unregister_Notification(IntPtr handle);
}
