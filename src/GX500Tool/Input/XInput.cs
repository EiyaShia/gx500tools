using System.Runtime.InteropServices;

namespace GX500Tool.Input;

[Flags]
public enum XButtons : ushort
{
    DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008,
    Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080,
    LeftShoulder = 0x0100, RightShoulder = 0x0200, Guide = 0x0400,
    A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000,
}

[StructLayout(LayoutKind.Sequential)]
public struct XInputGamepad
{
    public XButtons Buttons;
    public byte LeftTrigger, RightTrigger;
    public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
}

[StructLayout(LayoutKind.Sequential)]
public struct XInputState
{
    public uint PacketNumber;
    public XInputGamepad Gamepad;
}

[StructLayout(LayoutKind.Sequential)]
struct XInputVibration
{
    public ushort Left, Right;
}

public static class XInput
{
    [DllImport("xinput1_4.dll", EntryPoint = "#100")] // XInputGetStateEx: includes Guide button
    static extern int GetStateEx(int index, out XInputState state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    static extern int SetStateNative(int index, ref XInputVibration vib);

    [StructLayout(LayoutKind.Sequential)]
    struct BatteryInformation { public byte Type, Level; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    static extern int GetBatteryInformation(int index, byte devType, out BatteryInformation info);

    public static bool TryGetState(int index, out XInputState state) => GetStateEx(index, out state) == 0;

    /// <summary>Approximate battery (XInput only reports empty/low/medium/full). Null when wired or unknown.</summary>
    public static BatteryInfo? GetBattery(int index)
    {
        const byte Gamepad = 0, Disconnected = 0, Wired = 1, Unknown = 0xFF;
        if (GetBatteryInformation(index, Gamepad, out var b) != 0) return null;
        if (b.Type is Disconnected or Wired or Unknown) return null;
        int percent = b.Level switch { 0 => 0, 1 => 25, 2 => 60, _ => 100 };
        return new BatteryInfo(percent, Charging: false);
    }

    public static void SetVibration(int index, double left, double right)
    {
        var v = new XInputVibration { Left = (ushort)(left * 65535), Right = (ushort)(right * 65535) };
        SetStateNative(index, ref v);
    }
}
