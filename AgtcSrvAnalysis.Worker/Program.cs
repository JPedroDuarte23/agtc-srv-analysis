using Amazon.SQS;
using Amazon.SimpleNotificationService;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Driver;
using Prometheus;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Application.Services;
using AgtcSrvAnalysis.Infrastructure.Configuration;
using AgtcSrvAnalysis.Infrastructure.Repository;
using FiapSrvPayment.Infrastructure.Mappings;
using Microsoft.AspNetCore.Builder;
using AgtcSrvAnalysis.Worker;
using Microsoft.AspNetCore.Http;

[assembly: ExcludeFromCodeCoverage]

var builder = WebApplication.CreateBuilder(args);

Log.Logger = SerilogConfiguration.ConfigureSerilog();
builder.Host.UseSerilog();

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonSimpleSystemsManagement>();

string mongoConnectionString;
string jwtSigningKey;
string databaseName = builder.Configuration["MongoDbSettings:DatabaseName"]
    ?? throw new InvalidOperationException("Nome do Banco de Dados não configurado.");

if (!builder.Environment.IsDevelopment())
{
    Log.Information("Ambiente de Produção. Buscando segredos no AWS Parameter Store e S3.");

    var ssmClient = new AmazonSimpleSystemsManagementClient();

    var mongoParamName = builder.Configuration["ParameterStore:MongoDbConnection"];
    var mongoResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
    {
        Name = mongoParamName,
        WithDecryption = true
    });
    mongoConnectionString = mongoResponse.Parameter.Value;

    var jwtParamName = builder.Configuration["ParameterStore:JwtSigningKey"];
    var jwtResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
    {
        Name = jwtParamName,
        WithDecryption = true
    });
    jwtSigningKey = jwtResponse.Parameter.Value;

    var s3Bucket = builder.Configuration["DataProtection:S3BucketName"];
    var s3KeyPrefix = builder.Configuration["DataProtection:S3KeyPrefix"];

    if (!string.IsNullOrEmpty(s3Bucket) && !string.IsNullOrEmpty(s3KeyPrefix))
    {
        var s3DataProtectionConfig = new S3XmlRepositoryConfig(s3Bucket) { KeyPrefix = s3KeyPrefix };
        builder.Services.AddDataProtection()
            .SetApplicationName("AgroAnalysis")
            .PersistKeysToAwsS3(s3DataProtectionConfig);
    }
}
else
{
    Log.Information("Ambiente de Desenvolvimento. Usando configurações locais.");

    mongoConnectionString = builder.Configuration.GetConnectionString("MongoDbConnection")
        ?? throw new InvalidOperationException("Connection string MongoDbConnection não encontrada.");

    jwtSigningKey = builder.Configuration["Jwt:DevKey"]
        ?? throw new InvalidOperationException("Chave JWT de desenvolvimento não encontrada.");
}

builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
MongoMappings.ConfigureMappings();

builder.Services.AddScoped<ISensorDataRepository, SensorDataRepository>();
builder.Services.AddScoped<ITelemetryAnalyzer, TelemetryAnalyzer>();

builder.Services.AddHostedService<Worker>();

if (!string.IsNullOrEmpty(jwtSigningKey))
{
    builder.Services.ConfigureJwtBearer(builder.Configuration, jwtSigningKey);
    builder.Services.AddAuthorization();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseHttpMetrics();
app.MapMetrics();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Analysis.Worker" }));

try
{
    Log.Information("Iniciando Agro.Analysis.Worker...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Falha crítica na inicialização do Worker.");
}
finally
{
    Log.CloseAndFlush();
}