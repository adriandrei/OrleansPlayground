using Orleans.Configuration;

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

    silo.ConfigureEndpoints(
        siloPort: int.Parse(Environment.GetEnvironmentVariable("SiloPort") ?? "11111"),
        gatewayPort: 30000);

    silo.Configure<ReminderOptions>(o =>
    {
        o.RefreshReminderListPeriod = TimeSpan.FromMinutes(2);
        o.MinimumReminderPeriod = TimeSpan.FromSeconds(20);
    });

    silo.Configure<GrainCollectionOptions>(o =>
    {
        o.CollectionAge = TimeSpan.FromSeconds(10);
        o.CollectionQuantum = TimeSpan.FromSeconds(9);
    });

    silo.Configure<MessagingOptions>(options =>
    {
        options.ResponseTimeout = TimeSpan.FromMinutes(2);
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
        opt.DeleteStateOnClear = true;
    });

    silo.UseDashboard(opt =>
    {
        opt.HostSelf = false;
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.UseOrleansDashboard(new OrleansDashboard.DashboardOptions
{
    BasePath = "/dashboard"
});


app.Run();
