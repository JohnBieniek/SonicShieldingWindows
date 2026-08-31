namespace SonicShielding.Windows;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "SonicShielding.Windows.SingleInstance", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
