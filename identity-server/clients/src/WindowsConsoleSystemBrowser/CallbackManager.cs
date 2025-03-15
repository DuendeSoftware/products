// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IO.Pipes;

namespace WindowsConsoleSystemBrowser;

internal class CallbackManager
{
    private readonly string _name;

    public CallbackManager(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public int ClientConnectTimeoutSeconds { get; set; } = 1;

    public async Task RunClient(string args)
    {
        using (var client = new NamedPipeClientStream(".", _name, PipeDirection.Out))
        {
            await client.ConnectAsync(ClientConnectTimeoutSeconds * 1000);

            using (var sw = new StreamWriter(client) { AutoFlush = true })
            {
                await sw.WriteAsync(args);
            }
        }
    }

    public async Task<string> RunServer(CancellationToken? token = null)
    {
        token = CancellationToken.None;

        using (var server = new NamedPipeServerStream(_name, PipeDirection.In))
        {
            await server.WaitForConnectionAsync(token.Value);

            using (var sr = new StreamReader(server))
            {
                var msg = await sr.ReadToEndAsync();
                return msg;
            }
        }
    }
}
