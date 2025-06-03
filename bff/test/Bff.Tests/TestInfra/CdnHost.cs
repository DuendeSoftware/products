// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Tests.TestInfra;

public class CdnHost(TestHostContext context) : TestHost(context, new Uri("https://cdn"))
{
    public readonly string IndexHtml =
        """
            <html>
               <body>
                  <div>hi, i'm the index</div>
               </body>
            </html>
        """;


    public override void Initialize()
    {
        OnConfigure += app =>
        {
            app.UseRouting();

            app.UseAuthentication();
            // adds authorization for local and remote API endpoints
            app.UseAuthorization();
        };

        OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet("/index.html", () => IndexHtml);
        };
    }
}
