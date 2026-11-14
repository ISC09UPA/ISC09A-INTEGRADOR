using CasaDomotica.Home.Functions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient<HomeIngestionClient>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddSingleton<QuarantineStore>();
    })
    .Build();
await host.RunAsync();
