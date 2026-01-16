using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Domain.Entities;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace AgtcSrvAnalysis.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IAmazonSQS sqsClient, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = _configuration["AWS:SqsQueueUrl"];
        _logger.LogInformation($"Iniciando consumo da fila: {queueUrl}");


        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar fila SQS.");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message)
    {
        try
        {
            var sensorData = JsonSerializer.Deserialize<SensorData>(message.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (sensorData != null)
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var analyzer = scope.ServiceProvider.GetRequiredService<ITelemetryAnalyzer>();
                    await analyzer.AnalyzeAndPersistAsync(sensorData);
                }

                await _sqsClient.DeleteMessageAsync(_configuration["AWS:SqsQueueUrl"], message.ReceiptHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar mensagem individual ID: {MessageId}", message.MessageId);
        }
    }
}
