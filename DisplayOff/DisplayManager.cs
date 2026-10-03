namespace DisplayOff;

internal static class DisplayManager
{
    private const int MonitorPowerOff = 2;
    private const int PointerActivationFloorMs = 750;

    public static int ResolveDelay(DisplayOffDelay delay, bool fromPointer)
    {
        int milliseconds = delay.ToMilliseconds();
        if (fromPointer)
            milliseconds = Math.Max(milliseconds, PointerActivationFloorMs);

        return milliseconds;
    }

    public static void TurnOff()
    {
        NativeMethods.SendMessage(
            NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SYSCOMMAND,
            (IntPtr)NativeMethods.SC_MONITORPOWER,
            (IntPtr)MonitorPowerOff);
    }
}
