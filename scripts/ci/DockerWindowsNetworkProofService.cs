using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Disposable HTTP socket fixture for Windows Docker Desktop *host adapter*
// CI. Does not emulate the Docker daemon, images or storage.
public static class DockerWindowsNetworkProofService
{
    private sealed class SocketServer
    {
        private readonly TcpListener listener;
        private readonly Thread thread;

        internal SocketServer(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            thread = new Thread(Serve) { IsBackground = true };
            thread.Start();
        }

        private void Serve()
        {
            while (true)
            {
                try
                {
                    using (var client = listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 2500;
                        client.SendTimeout = 2500;
                        using (var stream = client.GetStream())
                        {
                            var incoming = new byte[2048];
                            stream.Read(incoming, 0, incoming.Length);
                            const string json = "{\"status\":\"ok\",\"service\":\"scada-api\"}";
                            var reply = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
                                "Content-Length: " + Encoding.UTF8.GetByteCount(json) +
                                "\r\nConnection: close\r\n\r\n" + json;
                            var data = Encoding.UTF8.GetBytes(reply);
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                        }
                    }
                }
                catch (SocketException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (IOException) { }
            }
        }

        internal void Stop() { listener.Stop(); thread.Join(1500); }
    }

    private static int ReadPort(string path, string name)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var index = line.IndexOf('=');
            if (index < 0 || line.Substring(0, index).Trim() != name) continue;
            int value;
            if (int.TryParse(line.Substring(index + 1).Trim(), out value) &&
                value >= 1 && value <= 65535) return value;
        }
        throw new InvalidOperationException("Registered test listener port is unavailable");
    }

    public static void Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathRooted(args[0]))
            throw new InvalidOperationException("Test fixture requires a registered absolute env path");
        SocketServer web = null;
        SocketServer api = null;
        var webPort = 0;
        var apiPort = 0;
        try
        {
            while (true)
            {
                try
                {
                    var nextWeb = ReadPort(args[0], "ELITESCADA_WEB_PORT");
                    var nextApi = ReadPort(args[0], "ELITESCADA_API_PORT");
                    if (nextWeb != webPort || nextApi != apiPort)
                    {
                        if (web != null) { web.Stop(); web = null; }
                        if (api != null) { api.Stop(); api = null; }
                        web = new SocketServer(nextWeb);
                        api = new SocketServer(nextApi);
                        webPort = nextWeb;
                        apiPort = nextApi;
                    }
                }
                catch (IOException) { /* The host executor swaps the file atomically. */ }
                catch (UnauthorizedAccessException) { }
                catch (SocketException) { }
                Thread.Sleep(50);
            }
        }
        finally
        {
            if (web != null) web.Stop();
            if (api != null) api.Stop();
        }
    }
}
