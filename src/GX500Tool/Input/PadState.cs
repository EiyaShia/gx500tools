namespace GX500Tool.Input;

/// <summary>Logical buttons in Xbox layout. Switch buttons are mapped by position (Switch B = A, etc.).</summary>
[Flags]
public enum PadButton : uint
{
    None = 0,
    A = 1 << 0, B = 1 << 1, X = 1 << 2, Y = 1 << 3,
    LB = 1 << 4, RB = 1 << 5, LT = 1 << 6, RT = 1 << 7,
    Back = 1 << 8, Start = 1 << 9, LS = 1 << 10, RS = 1 << 11, Guide = 1 << 12,
    Up = 1 << 13, Down = 1 << 14, Left = 1 << 15, Right = 1 << 16,
    Capture = 1 << 17,
}

/// <summary>Unified controller state: sticks -1..1 (Y up positive), triggers 0..1.</summary>
public struct PadState
{
    public PadButton Buttons;
    public double LX, LY, RX, RY;
    public double LT, RT;
    public ImuSample? Imu;

    public readonly bool IsPressed(PadButton b) => (Buttons & b) != 0;

    public static IEnumerable<PadButton> AllButtons =>
        Enum.GetValues<PadButton>().Where(b => b != PadButton.None);
}
