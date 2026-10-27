using System.Runtime.InteropServices;

namespace GX500Tool.Output;

/// <summary>Thin wrapper over user32 SendInput for keyboard and mouse events.</summary>
static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput { public int Dx, Dy; public uint Data, Flags, Time; public nint Extra; }

    [StructLayout(LayoutKind.Sequential)]
    struct KeybdInput { public ushort Vk, Scan; public uint Flags, Time; public nint Extra; }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MouseInput Mi; [FieldOffset(0)] public KeybdInput Ki; }

    [StructLayout(LayoutKind.Sequential)]
    struct Input { public uint Type; public InputUnion U; }

    [DllImport("user32.dll")] static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);

    public const uint MouseMove = 0x0001;
    public const uint LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008, RightUp = 0x0010, MiddleDown = 0x0020, MiddleUp = 0x0040;
    const uint KeyExtended = 0x0001, KeyUp = 0x0002, KeyScancode = 0x0008;

    public static void Mouse(uint flags, int dx = 0, int dy = 0) =>
        Send(new Input { Type = 0, U = { Mi = new MouseInput { Dx = dx, Dy = dy, Flags = flags } } });

    /// <summary>Sends a key by scancode, which games using DirectInput/raw input pick up more reliably than virtual keys.</summary>
    public static void Key(ushort vk, bool down, bool extended)
    {
        uint flags = KeyScancode | (down ? 0 : KeyUp) | (extended ? KeyExtended : 0);
        var scan = (ushort)MapVirtualKey(vk, 0);
        Send(new Input { Type = 1, U = { Ki = new KeybdInput { Scan = scan, Flags = flags } } });
    }

    static void Send(Input input) => SendInput(1, [input], Marshal.SizeOf<Input>());
}
