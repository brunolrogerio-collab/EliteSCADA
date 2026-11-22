using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class ScadivoTray
{
    private const string ServiceName = "ScadivoRuntime";

    [STAThread]
    private static void Main(string[] arguments)
    {
        var action = arguments.Length == 1 ? arguments[0] : "";
        if (action == "--start" || action == "--stop" || action == "--restart")
        {
            ControlService(action);
            return;
        }
        var port = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\SCADIVO\Windows", "WebPort", 18080);
        var url = "http://127.0.0.1:" + port;
        if (action == "--open") Open(url);
        using (var guard = new Mutex(false, @"Local\SCADIVO.Windows.Monitor"))
        {
            if (!guard.WaitOne(0)) return;
            Application.EnableVisualStyles();
            using (var artwork = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
            using (var menu = new ContextMenuStrip())
            using (var icon = new NotifyIcon { Icon = artwork ?? SystemIcons.Application, Visible = true, Text = "SCADIVO", ContextMenuStrip = menu })
            using (var timer = new System.Windows.Forms.Timer { Interval = 4000 })
            using (var context = new ApplicationContext())
            {
                var status = new ToolStripMenuItem("SCADIVO") { Enabled = false };
                menu.Items.Add(status);
                menu.Items.Add("Abrir SCADIVO", null, (sender, e) => Open(url));
                menu.Items.Add("Uso da licença", null, (sender, e) => Open(url + "/licensing"));
                menu.Items.Add(new ToolStripSeparator());
                AddAction(menu, "Iniciar serviço", "--start");
                AddAction(menu, "Parar serviço", "--stop");
                AddAction(menu, "Reiniciar serviço", "--restart");
                menu.Items.Add("Sair do monitor", null, (sender, e) => context.ExitThread());
                icon.DoubleClick += (sender, e) => Open(url);
                var pending = false;
                timer.Tick += async (sender, e) =>
                {
                    if (pending) return;
                    pending = true;
                    var text = "SCADIVO · indisponível";
                    try
                    {
                        var request = WebRequest.CreateHttp(url + "/health");
                        request.Proxy = null;
                        using (var timeout = new CancellationTokenSource(2000))
                        using (timeout.Token.Register(request.Abort))
                        using (var response = (HttpWebResponse)await request.GetResponseAsync())
                            if (response.StatusCode == HttpStatusCode.OK) text = "SCADIVO · em execução";
                    }
                    catch (WebException) {}
                    finally { pending = false; }
                    if (timer.Enabled) { status.Text = text; icon.Text = text; }
                };
                timer.Start();
                Application.Run(context);
                timer.Stop();
            }
            guard.ReleaseMutex();
        }
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Win32Exception ex) { MessageBox.Show(ex.Message, "SCADIVO"); }
    }

    private static void AddAction(ContextMenuStrip menu, string title, string action)
    {
        menu.Items.Add(title, null, (sender, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, action) {
                    Verb = "runas", UseShellExecute = true
                });
            }
            catch (Win32Exception ex) { if (ex.NativeErrorCode != 1223) MessageBox.Show(ex.Message, "SCADIVO"); }
        });
    }

    private static void ControlService(string action)
    {
        try
        {
            using (var service = new ServiceController(ServiceName))
            {
                if (action != "--start" && service.Status != ServiceControllerStatus.Stopped)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                }
                service.Refresh();
                if (action != "--stop" && service.Status != ServiceControllerStatus.Running)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
                }
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "SCADIVO"); }
    }
}
