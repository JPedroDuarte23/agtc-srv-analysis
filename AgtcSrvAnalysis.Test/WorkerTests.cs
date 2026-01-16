using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Domain.Entities;
using AgtcSrvAnalysis.Worker;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class WorkerTests
{
    private readonly Mock<ILogger<Worker>> _loggerMock;
    private readonly Mock<IAmazonSQS> _sqsClientMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<ITelemetryAnalyzer> _telemetryAnalyzerMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public WorkerTests()
    {
        _loggerMock = new Mock<ILogger<Worker>>();
        _sqsClientMock = new Mock<IAmazonSQS>();
        _configurationMock = new Mock<IConfiguration>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _telemetryAnalyzerMock = new Mock<ITelemetryAnalyzer>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ITelemetryAnalyzer))).Returns(_telemetryAnalyzerMock.Object);

        _configurationMock.Setup(x => x["AWS:SqsQueueUrl"]).Returns("test-queue-url");
    }

    private Worker CreateWorker()
    {
        return new Worker(
            _loggerMock.Object,
            _sqsClientMock.Object,
            _configurationMock.Object,
            _scopeFactoryMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessMessage_WhenMessageIsReceived()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var sensorData = new SensorData { Id = Guid.NewGuid(), FieldId = Guid.NewGuid(), SensorDeviceId = Guid.NewGuid(), SensorType = AgtcSrvAnalysis.Domain.Enum.SensorType.Temperatura, Timestamp = DateTime.UtcNow, Value = 100 };
        var message = new Message
        {
            MessageId = "test-message-id",
            Body = JsonSerializer.Serialize(sensorData),
            ReceiptHandle = "test-receipt-handle"
        };
        var receiveMessageResponse = new ReceiveMessageResponse
        {
            Messages = { message }
        };

        _sqsClientMock.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiveMessageResponse)
            .Returns(async () => {
                cts.Cancel();
                return new ReceiveMessageResponse();
            });

        _telemetryAnalyzerMock.Setup(x => x.AnalyzeAndPersistAsync(It.IsAny<SensorData>())).Returns(Task.CompletedTask);
        _sqsClientMock.Setup(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new DeleteMessageResponse());

        var worker = CreateWorker();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100, CancellationToken.None); // Give some time for the worker to process

        // Assert
        _telemetryAnalyzerMock.Verify(x => x.AnalyzeAndPersistAsync(It.IsAny<SensorData>()), Times.Once);
        _sqsClientMock.Verify(x => x.DeleteMessageAsync("test-queue-url", "test-receipt-handle", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogAndDelay_WhenSqsThrowsException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _sqsClientMock.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("SQS Error"))
            .Returns(async () => {
                cts.Cancel();
                return new ReceiveMessageResponse();
            });

        var worker = CreateWorker();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(1100, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao processar fila SQS.")),
                It.IsAny<System.Exception>(),
                It.IsAny<Func<It.IsAnyType, System.Exception, string>>()),
            Times.AtLeastOnce);
    }
    [Fact]
    public async Task ProcessMessageAsync_ShouldNotDeleteMessage_WhenAnalyzerThrowsException()
    {
        // Arrange
        var sensorData = new SensorData { Id = Guid.NewGuid(), FieldId = Guid.NewGuid(), SensorDeviceId = Guid.NewGuid(), SensorType = AgtcSrvAnalysis.Domain.Enum.SensorType.Temperatura, Timestamp = DateTime.UtcNow, Value = 100 };
        var message = new Message
        {
            MessageId = "test-message-id-fail",
            Body = JsonSerializer.Serialize(sensorData),
            ReceiptHandle = "test-receipt-handle-fail"
        };
        var receiveMessageResponse = new ReceiveMessageResponse { Messages = { message } };

        _sqsClientMock.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiveMessageResponse)
            .Returns(async () => new ReceiveMessageResponse());

        _telemetryAnalyzerMock.Setup(x => x.AnalyzeAndPersistAsync(It.IsAny<SensorData>()))
                              .ThrowsAsync(new System.Exception("Analyzer error"));

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100); // Allow time for processing
        cts.Cancel();

        // Assert
        _telemetryAnalyzerMock.Verify(x => x.AnalyzeAndPersistAsync(It.IsAny<SensorData>()), Times.Once);
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Falha ao processar mensagem individual ID: {message.MessageId}")),
                It.IsAny<System.Exception>(),
                It.IsAny<Func<It.IsAnyType, System.Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldNotProcess_WhenMessageBodyIsInvalid()
    {
        // Arrange
        var message = new Message
        {
            MessageId = "test-message-id-invalid",
            Body = "invalid-json",
            ReceiptHandle = "test-receipt-handle-invalid"
        };
        var receiveMessageResponse = new ReceiveMessageResponse { Messages = { message } };

        _sqsClientMock.SetupSequence(x => x.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiveMessageResponse)
            .Returns(async () => new ReceiveMessageResponse());

        var worker = CreateWorker();
        var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100); // Allow time for processing
        cts.Cancel();

        // Assert
        _telemetryAnalyzerMock.Verify(x => x.AnalyzeAndPersistAsync(It.IsAny<SensorData>()), Times.Never);
        _sqsClientMock.Verify(x => x.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Falha ao processar mensagem individual ID: {message.MessageId}")),
                It.IsAny<System.Exception>(),
                It.IsAny<Func<It.IsAnyType, System.Exception, string>>()),
            Times.Once);
    }
}
