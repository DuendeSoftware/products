using Clients;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleResourceIndicators
{
    public class Program
    {
        static OidcClient _oidcClient;
        
        public static async Task Main()
        {
            Console.WriteLine("+------------------------------+");
            Console.WriteLine("|  Resource Indicators Demo    |");
            Console.WriteLine("+------------------------------+");
            Console.WriteLine("");

            while (true)
            {
                Console.WriteLine("\n\n");

                var tests = new List<Tuple<string, string[]>>()
                {
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", null) },
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", null) },
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", new[] { "urn:resource1", "urn:resource2" })},
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", new[] { "urn:resource1", "urn:resource2" })},
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", new[] { "urn:resource1", "urn:resource2", "urn:resource3" })},
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", new[] { "urn:resource1", "urn:resource2", "urn:resource3" })},
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", new[] { "urn:resource3" })},
                    { new("resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", new[] { "urn:resource3" })},
                    { new("resource3.scope1 offline_access", new[] { "urn:resource3" })},
                    { new("resource3.scope1", new[] { "urn:resource3" })},
                    { new("resource1.scope1 offline_access", new[] { "urn:resource3" })},
                    { new("shared.scope", new[] { "urn:invalid" })},
                };

                var a = ConsoleKey.A;
                foreach(var test in tests)
                {
                    var resources = test.Item2 != null ? test.Item2.Aggregate((x, y) => $"{x}, {y}") : "-none-";
                    (a++.ToString() + ") SCOPES: " + test.Item1 + ", RESOURCES: " + resources).ConsoleGreen();
                }

                "x) exit".ConsoleGreen();
                var key = Console.ReadKey();

                if (key.Key == ConsoleKey.X) return;
                if (ConsoleKey.A <= key.Key && key.Key < ConsoleKey.X)
                {
                    var item = tests[key.Key - ConsoleKey.A];
                    try
                    {
                        await FrontChannel(item.Item1, item.Item2);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                    }
                }
            }
        }

        private static async Task FrontChannel(string scope, IEnumerable<string> resource)
        {
            resource = resource ?? Enumerable.Empty<string>();

            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new SystemBrowser();
            string redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");

            var options = new OidcClientOptions
            {
                Authority = Constants.Authority,

                ClientId = "console.resource.indicators",

                RedirectUri = redirectUri,
                Scope = scope,
                Resource = resource.ToList(),
                FilterClaims = false,
                LoadProfile = false,
                Browser = browser,
                
                Policy =
                {
                    RequireIdentityTokenSignature = false
                }
            };

            var serilog = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
                .CreateLogger();

            options.LoggerFactory.AddSerilog(serilog);

            _oidcClient = new OidcClient(options);
            var result = await _oidcClient.LoginAsync();

            var parts = result.AccessToken.Split('.');
            var header = parts[0];
            var payload = parts[1];

            Console.WriteLine();
            Console.WriteLine("Standard access token:");
            Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(header)).PrettyPrintJson());
            Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(payload)).PrettyPrintJson());

            if (result.RefreshToken == null)
            {
                Console.WriteLine();
                Console.WriteLine("No Refresh Token, exiting.");
                return;
            }

            await BackChannel(result);
        }

        private static async Task BackChannel(LoginResult result)
        {
            Console.WriteLine("\n\n");
            Console.WriteLine("Refresh with resource parameter");
            
            while (true)
            {
                Console.WriteLine("\n\n");

                "a) urn:resource1".ConsoleGreen();
                "b) urn:resource2".ConsoleGreen();
                "c) urn:resource3".ConsoleGreen();
                
                "x) exit".ConsoleGreen();
                var key = Console.ReadKey();

                switch (key.Key)
                {
                    case ConsoleKey.A:
                        await Refresh(result.RefreshToken, "urn:resource1");
                        break;
                    case ConsoleKey.B:
                        await Refresh(result.RefreshToken, "urn:resource2");
                        break;
                    case ConsoleKey.C:
                        await Refresh(result.RefreshToken, "urn:resource3");
                        break;
                    case ConsoleKey.X:
                        return;
                }
            }
        }

        private static async Task Refresh(string refreshToken, string resource)
        {
            var result =
                await _oidcClient.RefreshTokenAsync(refreshToken,
                    new Parameters
                    {
                        { "resource", resource }
                    });

            if (result.IsError)
            {
                Console.WriteLine();
                Console.WriteLine(result.Error);
                Console.ReadLine();
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine("down-scoped access token:");
            
            var parts = result.AccessToken.Split('.');
            var header = parts[0];
            var payload = parts[1];
            
            Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(header)).PrettyPrintJson());
            Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(payload)).PrettyPrintJson());

            Console.ReadLine();
        }
    }
}