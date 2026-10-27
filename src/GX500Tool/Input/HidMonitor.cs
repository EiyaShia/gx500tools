using HidSharp;

namespace GX500Tool.Input;

public record ImuSample(short AccX, short AccY, short AccZ, short GyroX, short GyroY, short GyroZ);

/// <summary>
/// Reads raw HID reports from a game controller (DInput / Switch mode).
/// For Switch Pro-compatible devices, enables the IMU and full report mode (0x30).
/// </summary>
public sealed class HidMonitor : IDisposable
{
    readonly HidDevice _device;
    HidStream? _stream;
    Thread? _thread;
    volatile bool _running;
    byte _counter;

    public event Action<byte[]>? ReportReceived;
    public event Action<ImuSample>? ImuReceived;

    public HidMonitor(HidDevice device) => _device = device;

    public static IEnumerable<HidDevice> FindControllers() =>
        DeviceList.Local.GetHidDevices().Where(d =>
        {
            try
            {
                return d.GetReportDescriptor().DeviceItems.Any(i =>
                    i.Usages.GetAllValues().Any(u => u == 0x00010004 || u == 0x00010005)); // Joystick / Gamepad
            }
            catch { return false; }
        });

    public static IEnumerable<HidDevice> FindAll() => DeviceList.Local.GetHidDevices();

    public static string Describe(HidDevice d)
    {
        string name;
        try { name = d.GetProductName(); } catch { name = "Unknown"; }
        string usage;
        try
        {
            var u = d.GetReportDescriptor().DeviceItems.SelectMany(i => i.Usages.GetAllValues()).FirstOrDefault();
            usage = $"  usage {u >> 16:X4}:{u & 0xFFFF:X4}";
        }
        catch { usage = ""; }
        return $"{name}  [VID {d.VendorID:X4} PID {d.ProductID:X4}]{usage}  in {d.GetMaxInputReportLength()}B";
    }

    public void Start()
    {
        _stream = _device.Open();
        _stream.ReadTimeout = 500;
        _stream.WriteTimeout = 500;
        _running = true;
        // Start reading first so replies to the handshake are drained, not buffered.
        _thread = new Thread(ReadLoop) { IsBackground = true, Name = "HID reader" };
        _thread.Start();
        TryEnableSwitchImu();
    }

    public bool IsSwitchPro => _device.VendorID == 0x057E;

    void TryEnableSwitchImu()
    {
        if (!IsSwitchPro) return;
        try
        {
            // USB handshake (Pro Controller over USB stays silent without it):
            // 0x80 0x02 handshake, 0x80 0x03 high baudrate, 0x80 0x02 again, 0x80 0x04 force USB HID mode.
            foreach (var cmd in new byte[] { 0x02, 0x03, 0x02, 0x04 })
            {
                SendRaw([0x80, cmd]);
                Thread.Sleep(60);
            }
            // Subcommand 0x03 = input report mode (0x30 = full, includes IMU), 0x40 = enable IMU.
            SendSubcommand(0x03, 0x30);
            Thread.Sleep(60);
            SendSubcommand(0x40, 0x01);
        }
        catch { /* device rejected output reports; raw view still works */ }
    }

    int OutputLength => Math.Max(_device.GetMaxOutputReportLength(), 64);

    void SendRaw(byte[] data)
    {
        var buf = new byte[OutputLength];
        data.CopyTo(buf, 0);
        _stream!.Write(buf);
    }

    void SendSubcommand(byte cmd, byte arg)
    {
        var len = OutputLength;
        var buf = new byte[len];
        buf[0] = 0x01;
        buf[1] = (byte)(_counter++ & 0x0F);
        byte[] neutralRumble = [0x00, 0x01, 0x40, 0x40, 0x00, 0x01, 0x40, 0x40];
        neutralRumble.CopyTo(buf, 2);
        buf[10] = cmd;
        buf[11] = arg;
        _stream!.Write(buf);
    }

    void ReadLoop()
    {
        var buf = new byte[_device.GetMaxInputReportLength()];
        while (_running)
        {
            int n;
            try { n = _stream!.Read(buf); }
            catch { if (!_running) break; continue; }
            if (n <= 0) continue;

            var report = buf[..n];
            ReportReceived?.Invoke(report);

            // Switch full report: ID 0x30, first IMU sample at offset 13 (accel xyz, gyro xyz as int16 LE).
            if (report[0] == 0x30 && n >= 25)
            {
                short S(int o) => BitConverter.ToInt16(report, o);
                ImuReceived?.Invoke(new ImuSample(S(13), S(15), S(17), S(19), S(21), S(23)));
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _stream?.Dispose();
    }
}
