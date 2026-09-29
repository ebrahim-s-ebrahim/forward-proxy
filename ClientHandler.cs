using System.Net.Sockets;
using System.Text;

namespace ForwardProxy;

public static class ClientHandler
{
    public static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                byte[] buffer = new byte[4096];
                var stream = client.GetStream();
                int n = await stream.ReadAsync(buffer);
                var text = Encoding.ASCII.GetString(buffer, 0, n);
                var firstLine = text.Split("\r\n")[0];
                var parts = firstLine.Split(' ');

                // METHOD TARGET VERSION
                if (parts.Length != 3)
                {
                    Console.WriteLine($"Bad request line: '{firstLine}'");
                    await SendStatusAsync(stream, "400 Bad Request");
                    return;
                }

                var method = parts[0];

                if (method != "CONNECT")
                {
                    Console.WriteLine("Not Supported Yet.");
                    return;
                }

                if (!TryParseTarget(parts[1], out var host, out var port))
                {
                    Console.WriteLine($"Bad CONNECT target: '{parts[1]}'");
                    await SendStatusAsync(stream, "400 Bad Request");
                    return;
                }

                Console.WriteLine($"{method} {host}:{port}");

                using var server = new TcpClient();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await server.ConnectAsync(host, port, cts.Token);
                    Console.WriteLine($"Connected to {host}:{port} successfully!");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Timed out after 5s connecting to {host}:{port}");
                    await SendStatusAsync(stream, "502 Bad Gateway");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect to {host}:{port}: {ex.Message}");
                    await SendStatusAsync(stream, "502 Bad Gateway");
                    return;
                }

                await SendStatusAsync(stream, "200 Connection Established");

                var serverStream = server.GetStream();
                var up = stream.CopyToAsync(serverStream);
                var down = serverStream.CopyToAsync(stream);
                // WhenAny and not WhenAll; because when one side hangs up, the other side 
                // should too; to avoid connection leaks 
                await Task.WhenAny(up, down);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Handler error: {ex}");
        }
    }

    static bool TryParseTarget(string target, out string host, out int port)
    {
        host = "";
        port = 0;

        var hostAndPort = target.Split(':');
        if (hostAndPort.Length != 2)
            return false;

        if (hostAndPort[0].Length == 0)
            return false;

        if (!int.TryParse(hostAndPort[1], out port) || port < 1 || port > 65535)
            return false;

        host = hostAndPort[0];
        return true;
    }

    static async Task SendStatusAsync(NetworkStream stream, string status)
    {
        var bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n\r\n");
        await stream.WriteAsync(bytes);
    }
}