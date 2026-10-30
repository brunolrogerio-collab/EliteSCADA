// Disposable SCM fixture, not the SCADIVO host or a replacement implementation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

public sealed class NativePortProofService : ServiceBase
{
    private readonly string configuration;
    private HttpListener listener;
    public NativePortProofService(string name, string path) { ServiceName = name; configuration = path; }
    protected override void OnStart(string[] args)
    {
        var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(configuration));
        var kestrel = (Dictionary<string, object>)root["Kestrel"];
        var endpoints = (Dictionary<string, object>)kestrel["Endpoints"];
        listener = new HttpListener();
        foreach (var name in new[] { "Web", "Api" })
        {
            var endpoint = (Dictionary<string, object>)endpoints[name];
            listener.Prefixes.Add(((string)endpoint["Url"]).TrimEnd('/') + "/");
        }
        listener.Start();
        ThreadPool.QueueUserWorkItem(delegate
        {
            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();
                    var health = context.Request.Url.AbsolutePath == "/health";
                    context.Response.StatusCode = health ? 200 : 404;
                    context.Response.ContentType = "application/json";
                    var bytes = Encoding.UTF8.GetBytes(health ? "{\"status\":\"ok\",\"service\":\"scada-api\"}" : "{}");
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        });
    }
    protected override void OnStop() { if (listener != null) listener.Close(); }
    public static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Service identity and configuration required");
        ServiceBase.Run(new NativePortProofService(args[0], args[1]));
    }
}
