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

    // Example: 1x1 transparent PNG
    public readonly byte[] ImageBytes = new byte[]
    {
        137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82,0,0,0,1,0,0,0,1,8,6,0,0,0,31,21,196,137,
        0,0,0,13,73,68,65,84,8,153,99,0,1,0,0,5,0,1,13,10,26,10,0,0,0,0,73,69,78,68,174,66,96,130
    };

    public override void Initialize() => OnConfigureApp +=
        app =>
        {
            app.UseAuthentication();
            // adds authorization for local and remote API endpoints
            app.UseAuthorization();

            app.MapGet("/", () => Results.Content(IndexHtml, "text/html"));
            app.MapGet("/index.html", () => Results.Content(IndexHtml, "text/html"));
            app.MapGet("/index2.html", () => Results.Content(IndexHtml, "text/html"));
            app.MapGet("/image.png", () => Results.File(ImageBytes, "image/png"));
        };

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();
        base.ConfigureApp(app);
    }
}
