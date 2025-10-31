using Microsoft.AspNetCore.OpenApi;
using Orleans.Configuration;
using OrleansPlayground;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var cosmosConnectionString =
    "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

builder.Host.UseOrleans(silo =>
{
    silo.Configure<ClusterOptions>(opt =>
    {
        opt.ClusterId = "dev";
        opt.ServiceId = "ReminderPlayground";
    });

    silo.ConfigureEndpoints(siloPort: int.Parse(Environment.GetEnvironmentVariable("SiloPort")), gatewayPort: 30000);

    silo.Configure<ReminderOptions>(o =>
    {
        o.RefreshReminderListPeriod = TimeSpan.FromMinutes(2);
        o.MinimumReminderPeriod = TimeSpan.FromSeconds(10);
    });

    silo.Configure<GrainCollectionOptions>(o =>
    {
        o.CollectionAge = TimeSpan.FromSeconds(6);
        o.CollectionQuantum = TimeSpan.FromSeconds(5);
    });

    silo.UseCosmosClustering(opt =>
    {
        opt.ConfigureCosmosClient(cosmosConnectionString);
        opt.DatabaseName = "devops";
        opt.ContainerName = "orleans-clustering";
        opt.IsResourceCreationEnabled = true;
    });

    silo.UseCosmosReminderService(opt =>
    {
        opt.ConfigureCosmosClient(cosmosConnectionString);
        opt.DatabaseName = "devops";
        opt.ContainerName = "orleans-reminders";
        opt.IsResourceCreationEnabled = true;
    });

    silo.AddCosmosGrainStorage("catalogStore", opt =>
    {
        opt.ConfigureCosmosClient(cosmosConnectionString);
        opt.DatabaseName = "devops";
        opt.ContainerName = "orleans-catalog";
        opt.IsResourceCreationEnabled = true;
    });


    silo.UseDashboard(opt => opt.HostSelf = true);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseOrleansDashboard(new OrleansDashboard.DashboardOptions { BasePath = "/dashboard" });

app.Run();
