using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Application.Services;
using AgtcSrvAnalysis.Domain.Entities;
using AgtcSrvAnalysis.Domain.Enum; // Certifique-se que o Enum está aqui
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AgtcSrvAnalysis.Test.Services
{
    public class TelemetryAnalyzerTests
    {
        private readonly Mock<ISensorDataRepository> _repositoryMock;
        private readonly TelemetryAnalyzer _analyzer;

        public TelemetryAnalyzerTests()
        {
            _repositoryMock = new Mock<ISensorDataRepository>();
            _analyzer = new TelemetryAnalyzer(_repositoryMock.Object);
        }

        [Fact]
        public async Task AnalyzeAndPersistAsync_ShouldAlwaysCallRepositoryAddAsync()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Temperatura, // Simulando string ou Enum.ToString()
                Value = 25.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            };

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            _repositoryMock.Verify(r => r.AddAsync(sensorData), Times.Once);
        }

        // =================================================================
        // TESTES DE UMIDADE (SECA)
        // =================================================================

        [Fact]
        public async Task AnalyzeAndPersistAsync_Humidity_ShouldTriggerDroughtAlert_WhenBelow30()
        {
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Umidade, // Simulando string ou Enum.ToString()
                Value = 25.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            };

            // Captura o Console
            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("SECA", output); // Verifica palavra-chave
            Assert.Contains("ALERTA", output);
            _repositoryMock.Verify(r => r.AddAsync(sensorData), Times.Once);
        }

        [Fact]
        public async Task AnalyzeAndPersistAsync_Humidity_ShouldNotTriggerAlert_WhenAbove30()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Umidade, // Simulando string ou Enum.ToString()
                Value = 40.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            };

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.DoesNotContain("SECA", output);
            Assert.DoesNotContain("ALERTA", output);
        }

        // =================================================================
        // TESTES DE TEMPERATURA (GEADA E CALOR)
        // =================================================================

        [Fact]
        public async Task AnalyzeAndPersistAsync_Temperature_ShouldTriggerFrostAlert_WhenBelow5()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Temperatura,
                Value = 4.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            }; // Geada (< 5)

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("GEADA", output);
        }

        [Fact]
        public async Task AnalyzeAndPersistAsync_Temperature_ShouldTriggerHeatAlert_WhenAbove35()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Temperatura,
                Value = 36.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            }; // Calor (> 35)

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("CALOR EXCESSIVO", output);
        }

        [Fact]
        public async Task AnalyzeAndPersistAsync_Temperature_ShouldNotTriggerAlert_WhenNormal()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Temperatura,
                Value = 25.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            }; // Normal (5 a 35)

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.DoesNotContain("ALERTA", output);
        }

        // =================================================================
        // TESTES DE PRESSÃO (TEMPESTADE)
        // =================================================================

        [Fact]
        public async Task AnalyzeAndPersistAsync_Pressure_ShouldTriggerStormAlert_WhenBelow1000()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Pressao,
                Value = 400.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            }; // Tempestade (< 1000)

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("TEMPESTADE", output);
            Assert.Contains("BAIXA PRESSÃO", output);
        }

        [Fact]
        public async Task AnalyzeAndPersistAsync_Pressure_ShouldNotTriggerAlert_WhenNormal()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                SensorType = SensorType.Pressao,
                Value = 1013.0,
                FieldId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                fieldName = "Campo Teste",
                propertyName = "Propriedade Teste",
                farmerName = "Fazendeiro Teste"
            }; // Normal

            using var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            await _analyzer.AnalyzeAndPersistAsync(sensorData);

            // Assert
            var output = consoleOutput.ToString();
            Assert.DoesNotContain("TEMPESTADE", output);
        }
    }
}