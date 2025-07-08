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


    public override void Initialize() => OnConfigureApp += app =>
                                              {
                                                  app.UseAuthentication();
                                                  // adds authorization for local and remote API endpoints
                                                  app.UseAuthorization();

                                                  app.MapGet("/index.html", () => IndexHtml);
                                              };

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();
        base.ConfigureApp(app);
    }
}
