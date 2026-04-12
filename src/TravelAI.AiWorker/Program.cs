using MassTransit;
using Serilog;
using TravelAI.Core.Extensions;
using TravelAI.AiWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(builder.Configuration)
       .WriteTo.Console(outputTemplate:
           "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddTravelAI(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ItineraryRequestedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

var host = builder.Build();
host.Run();