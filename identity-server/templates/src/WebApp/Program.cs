using TemplateWebApp;

try
{
    var builder = WebApplication
        .CreateBuilder(args);

    builder
        .ConfigureServices()
        .ConfigurePipeline()
        .Run();
}
catch (Exception ex)
{
    Console.WriteLine("Unhandled exception: " + ex);
}
finally
{
    Console.WriteLine("Shut down complete");
}
