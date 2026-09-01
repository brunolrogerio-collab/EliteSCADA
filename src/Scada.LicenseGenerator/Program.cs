using System.Runtime.InteropServices;

namespace EliteSCADA.LicenseGenerator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(argument => argument.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase)))
            return 0;

        var forceGui = args.Any(argument => argument.Equals("--gui", StringComparison.OrdinalIgnoreCase));
        if (args.Length > 0 && !forceGui)
        {
            ConsoleBridge.AttachParent();
            return LicenseGeneratorCli.Run(args);
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LicenseGeneratorForm());
        return 0;
    }
}

internal static class ConsoleBridge
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static void AttachParent()
    {
        if (!OperatingSystem.IsWindows() || !AttachConsole(AttachParentProcess))
            return;

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
}
