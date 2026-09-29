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

                ValidateRequest(parts);

                var method = parts[0];

                if (method != "CONNECT")
                {
                    Console.WriteLine("Not Supported Yet.");
                    return;
                }

                var target = parts[1];
                var hostAndPort = target.Split(':');
                var host = hostAndPort[0];
                var port = int.Parse(hostAndPort[1]);

                Console.WriteLine($"{method} {host}:{port}");

                using var server = new TcpClient();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await server.ConnectAsync(host, port, cts.Token);
                    Console.WriteLine($"Connected to {host}:{port} successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Timed out after 5s, failed to connect to {host}:{port}: {ex.Message}");
                    var failedReply = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                    await stream.WriteAsync(failedReply);
                    return;
                }

                var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                await stream.WriteAsync(reply);

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

    static void ValidateRequest(string[] reqParts)
    {
        //   - Does parts have at least 3 elements (method, target, version)?

        //   - Does the target contain exactly one :?

        //   - Is the port really a number? Use int.TryParse instead of int.Parse: it returns
        //     false instead of throwing.
    }
}