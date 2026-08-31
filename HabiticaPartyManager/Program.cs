using HabiticaPartyManager;
using HabiticaPartyManager.Habitica;
using HabiticaPartyManager.Options;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "HabiticaPartyManager");

builder.Services.AddSerilog(config => config
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true));

builder.Services.AddOptions<HabiticaOptions>()
    .Bind(builder.Configuration.GetSection(HabiticaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<HabiticaClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<HabiticaOptions>>().CurrentValue;

    client.BaseAddress = new Uri("https://habitica.com/api/v3/");
    client.DefaultRequestHeaders.Add("x-client", $"{options.UserId}-HabiticaPartyManager");
    client.DefaultRequestHeaders.Add("x-api-user", options.UserId);
    client.DefaultRequestHeaders.Add("x-api-key", options.ApiToken);
});

builder.Services.AddHostedService<InvitePollingService>();
builder.Services.AddHostedService<PartyMaintenanceService>();

var host = builder.Build();
host.Run();