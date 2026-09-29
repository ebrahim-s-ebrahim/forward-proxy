using System.Net;
using System.Net.Sockets;
using ForwardProxy;

var listener = new TcpListener(IPAddress.Loopback, 12345);
listener.Start();
Console.WriteLine($"Listening on {listener.LocalEndpoint}");

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    // I know this returns a Task, but I'm choosing not to wait for it
    _ = ClientHandler.HandleClientAsync(client);
}

