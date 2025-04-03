using UpsServer;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransient<HAClient>();
builder.Services.AddHostedService<HomeAssistantService>();
builder.Services.AddHostedService<UpsService>();

IHost host = builder.Build();
host.Run();
