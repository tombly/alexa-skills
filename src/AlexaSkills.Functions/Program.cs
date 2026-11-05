using AlexaSkills.Alexa;
using AlexaSkills.Skills.MajorSystem;
using AlexaSkills.Skills.TimeClock;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Core Tools sets this when running locally; Azure sets it to Production.
var isLocalDevelopment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development";

builder.Services.AddOptions<AlexaOptions>()
    .Bind(builder.Configuration.GetSection("Alexa"))
    .PostConfigure(options =>
    {
        // Request checks can only be switched off on a developer machine.
        if (!isLocalDevelopment)
        {
            options.VerifyRequests = true;
        }
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(AlexaRequestVerifier.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.MaxResponseContentBufferSize = 64 * 1024;
});
builder.Services.AddSingleton<AlexaRequestVerifier>();
builder.Services.AddSingleton<SkillEndpoint>();

builder.Services.AddSingleton(services => CreateTimeClockTable(
    builder.Configuration, isLocalDevelopment, services.GetRequiredService<ILogger<TableTimeCardStore>>()));
builder.Services.AddSingleton<ITimeCardStore, TableTimeCardStore>();
builder.Services.AddSingleton<TimeClockSkill>();
builder.Services.AddSingleton<MajorSystemSkill>();

builder.Build().Run();

static TableClient CreateTimeClockTable(IConfiguration config, bool isLocalDevelopment, ILogger logger)
{
    var tableName = config["TimeClock:TableName"] ?? "TimeClockUserData";

    // Locally, a connection string to Azurite; in Azure, the table endpoint and the app's managed identity.
    var connectionString = config["TimeClock:TableConnectionString"];
    if (!string.IsNullOrEmpty(connectionString))
    {
        var localTable = new TableClient(connectionString, tableName);
        if (isLocalDevelopment)
        {
            try
            {
                localTable.CreateIfNotExists();
            }
            catch (Exception ex) when (ex is RequestFailedException or AggregateException)
            {
                // Time Clock still answers what doesn't need storage, and apologizes for the rest.
                logger.LogWarning("Couldn't create the {Table} table. Is Azurite running?", tableName);
            }
        }
        return localTable;
    }

    var endpoint = config["TimeClock:TableEndpoint"]
        ?? throw new InvalidOperationException("Set TimeClock:TableEndpoint, or TimeClock:TableConnectionString for local development.");
    return new TableClient(new Uri(endpoint), tableName, new DefaultAzureCredential());
}
