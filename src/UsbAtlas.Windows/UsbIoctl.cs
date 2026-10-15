using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UsbAtlas.Windows;

// Only GET requests are exposed. USB IOCTL structures are packed to one byte.
internal sealed class UsbIoctl : IDisposable
{
    private readonly SafeFileHandle handle;
    public UsbIoctl(string path)
    {
        var normalized = path.StartsWith("\\\\") ? path : "\\\\.\\" + path;
        handle = NativeMethods.CreateFileW(normalized, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the USB handle.");
    }
    public byte[] Get(int function, int size, int index = 0, Action<byte[]>? prepare = null)
    {
        if (function is not (258 or 260 or 261 or 264 or 274 or 276 or 278 or 279))
            throw new ArgumentOutOfRangeException(nameof(function));
        var buffer = new byte[size];
        Put(buffer, 0, index);
        prepare?.Invoke(buffer);
        var code = 0x220000u | ((uint)function << 2);
        if (!NativeMethods.DeviceIoControl(handle, code, buffer, (uint)buffer.Length, buffer, (uint)buffer.Length, out var returned, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"USB query {function} is unavailable.");
        if (returned > buffer.Length) throw new InvalidDataException("Invalid USB response length.");
        return buffer[..(int)returned];
    }
    public string? Name(int function, int index = 0)
    {
        var data = Get(function, 8192, index);
        var offset = function == 258 ? 4 : 8;
        if (data.Length < offset + 2) return null;
        return Encoding.Unicode.GetString(data, offset, (data.Length - offset) & ~1).TrimEnd('\0');
    }
    public byte[] Descriptor(int port, byte type, byte index, ushort language = 0, int length = 4096)
    {
        var data = Get(260, length + 12, port, b =>
        {
            b[4] = 0x80; b[5] = 6; b[6] = index; b[7] = type;
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8), language);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(10), (ushort)length);
        });
        if (data.Length < 14) throw new InvalidDataException("Descriptor response is too short.");
        return data[12..];
    }
    public string? StringDescriptor(int port, byte index, ushort language)
    {
        if (index == 0) return null;
        var b = Descriptor(port, 3, index, language, 255);
        if (b.Length < 2 || b[1] != 3 || b[0] < 2 || b[0] > b.Length || b[0] % 2 != 0) return null;
        return Encoding.Unicode.GetString(b, 2, b[0] - 2).TrimEnd('\0');
    }
    internal static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    internal static ushort U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
    internal static void Put(byte[] b, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), value);
    public void Dispose() => handle.Dispose();
}
