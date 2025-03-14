// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;

namespace ConsoleScopesResources;

class Program
{
    private static DiscoveryCache Cache;

    static async Task Main(string[] args)
    {
        Console.Title = "Console Resources and Scopes Client";
        Cache = new DiscoveryCache("https://localhost:5001");

        var leave = false;

        while (leave == false)
        {
            Console.Clear();

            "Resource setup:\n".ConsoleGreen();

            "resource1: resource1.scope1 resource1.scope2 shared.scope".ConsoleGreen();
            "resource2: resource2.scope1 resource2.scope2 shared.scope\n".ConsoleGreen();
            "resource3 (isolated): resource3.scope1 resource3.scope2 shared.scope\n".ConsoleGreen();
            "scopes without resource association: scope3 scope4 transaction\n\n".ConsoleGreen();


            // scopes without associated resource
            "a) scope3 scope4".ConsoleYellow();

            // one scope, single resource
            "b) resource1.scope1".ConsoleYellow();

            // two scopes, single resources
            "c) resource1.scope1 resource1.scope2".ConsoleYellow();

            // two scopes, one has a resource, one doesn't
            "d) resource1.scope1 scope3".ConsoleYellow();

            // two scopes, two resource
            "e) resource1.scope1 resource2.scope1".ConsoleYellow();

            // shared scope between two resources
            "f) shared.scope".ConsoleYellow();

            // shared scope between two resources and scope that belongs to resource
            "g) resource1.scope1 shared.scope".ConsoleYellow();

            // parameterized scope
            "h) transaction:123".ConsoleYellow();

            // no scope
            "i) no scope".ConsoleYellow();

            // no scope
            "j) no scope (resource: resource1)".ConsoleYellow();

            // no scope
            "k) no scope (resource: resource3)".ConsoleYellow();

            // isolated scope without resource parameter
            "l) resource3.scope1".ConsoleYellow();

            // isolated scope without resource parameter
            "m) resource3.scope1 (resource: resource3)".ConsoleYellow();

            // isolated scope without resource parameter
            "n) resource3.scope1 (resource: resource2)".ConsoleYellow();

            "\nx) quit".ConsoleYellow();

            var input = Console.ReadKey();

            switch (input.Key)
            {
                case ConsoleKey.A:
                    await RequestToken("scope3 scope4");
                    break;

                case ConsoleKey.B:
                    await RequestToken("resource1.scope1");
                    break;

                case ConsoleKey.C:
                    await RequestToken("resource1.scope1 resource1.scope2");
                    break;

                case ConsoleKey.D:
                    await RequestToken("resource1.scope1 scope3");
                    break;

                case ConsoleKey.E:
                    await RequestToken("resource1.scope1 resource2.scope1");
                    break;

                case ConsoleKey.F:
                    await RequestToken("shared.scope");
                    break;

                case ConsoleKey.G:
                    await RequestToken("resource1.scope1 shared.scope");
                    break;

                case ConsoleKey.H:
                    await RequestToken("transaction:123");
                    break;

                case ConsoleKey.I:
                    await RequestToken("");
                    break;

                case ConsoleKey.J:
                    await RequestToken("", "urn:resource1");
                    break;

                case ConsoleKey.K:
                    await RequestToken("", "urn:resource3");
                    break;

                case ConsoleKey.L:
                    await RequestToken("resource3.scope1");
                    break;

                case ConsoleKey.M:
                    await RequestToken("resource3.scope1", "urn:resource3");
                    break;

                case ConsoleKey.N:
                    await RequestToken("resource3.scope1", "urn:resource2");
                    break;

                case ConsoleKey.X:
                    leave = true;
                    break;
            }
        }
    }

    static async Task RequestToken(string scope, string resource = null)
    {
        var client = new HttpClient();
        var disco = await Cache.GetAsync();

        var request = new ClientCredentialsTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = "console.resource.scope",
            ClientSecret = "secret",

            Scope = scope
        };

        if (!string.IsNullOrEmpty(resource))
        {
            request.Resource.Add(resource);
        }

        var response = await client.RequestClientCredentialsTokenAsync(request);

        if (response.IsError)
        {
            Console.WriteLine();
            Console.WriteLine(response.Error);
            Console.ReadLine();
            return;
        }

        Console.WriteLine();
        Console.WriteLine();

        response.Show();
        Console.ReadLine();
    }
}