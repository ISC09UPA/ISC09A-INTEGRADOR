using CasaDomotica.Notifications.Application;
using CasaDomotica.Notifications.Functions;
using CasaDomotica.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var operatorFlags = new[] { "--migrate", "--provision-runtime" };
if (args.Any(operatorFlags.Contains))
{
    var opBuilder = Host.CreateApplicationBuilder(args.Where(x => !operatorFlags.Contains(x)).ToArray());
    ConfigureDatabase(opBuilder.Services, opBuilder.Configuration);
    opBuilder.Services.AddScoped<NotificationsAdministration>();
    using var opHost = opBuilder.Build();
    using var scope = opHost.Services.CreateScope();
    var admin = scope.ServiceProvider.GetRequiredService<NotificationsAdministration>();
    if (args.Contains("--migrate")) await admin.MigrateAsync(CancellationToken.None);
    if (args.Contains("--provision-runtime")) await admin.ProvisionRuntimeAsync(opBuilder.Configuration["Runtime:Password"] ?? "", CancellationToken.None);
    Console.WriteLine("Notifications database operation completed.");
    return;
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        ConfigureDatabase(services, context.Configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IdentityKey>();
        services.AddSingleton<NotificationsServiceTokenCache>();
        services.AddHttpClient<INotificationsAuthorizer, NotificationsAuthorizer>(client => client.Timeout = TimeSpan.FromSeconds(45));
        services.AddHttpClient<IEmailProvider, AcsEmailProvider>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<INotificationsStore, SqlNotificationsStore>();
        services.AddScoped<NotificationOperations>();
    })
    .Build();

// Validate signing key configuration eagerly at host startup unless running tests that override DI.
_ = host.Services.GetRequiredService<IdentityKey>();
await host.RunAsync();

static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration) =>
    services.AddDbContext<NotificationsDb>(options =>
        options.UseSqlServer(
            configuration.GetConnectionString("NotificationsDb") ?? throw new InvalidOperationException("Notifications database is required."),
            sql => sql.CommandTimeout(90)));
