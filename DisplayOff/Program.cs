namespace DisplayOff;

internal static class Program
{
    private const string MutexName = @"Local\DisplayOff.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(static arg => string.Equals(arg, "--off", StringComparison.OrdinalIgnoreCase)))
        {
            TurnOffAndExit();
            return;
        }

        using Mutex? instance = AcquireSingleInstance();
        if (instance is null)
            return;

        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "DisplayOff-error.txt"), ex.ToString());
            }
            catch (IOException)
            {
            }

            MessageBox.Show(ex.Message, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void TurnOffAndExit()
    {
        AppSettings settings = SettingsManager.Load();
        int delay = DisplayManager.ResolveDelay(settings.Delay, fromPointer: false);
        if (delay > 0)
            Thread.Sleep(delay);

        DisplayManager.TurnOff();
    }

    private static Mutex? AcquireSingleInstance()
    {
        var mutex = new Mutex(false, MutexName, out _);
        try
        {
            if (mutex.WaitOne(0))
                return mutex;
        }
        catch (AbandonedMutexException)
        {
            return mutex;
        }

        mutex.Dispose();
        return null;
    }
}
