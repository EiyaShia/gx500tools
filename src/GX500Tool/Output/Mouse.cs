namespace GX500Tool.Output;

/// <summary>Relative mouse movement, keeping sub-pixel remainders so slow motion isn't lost.</summary>
public sealed class Mouse
{
    double _remX, _remY;

    public void Move(double dx, double dy)
    {
        _remX += dx; _remY += dy;
        int ix = (int)_remX, iy = (int)_remY;
        if (ix == 0 && iy == 0) return;
        _remX -= ix; _remY -= iy;
        NativeInput.Mouse(NativeInput.MouseMove, ix, iy);
    }
}
