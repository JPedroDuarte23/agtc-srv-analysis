using AgtcSrvAnalysis.Domain.Entities;
using AgtcSrvAnalysis.Infrastructure.Repository;
using MongoDB.Driver;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace AgtcSrvAnalysis.Test.Repository
{
    public class SensorDataRepositoryTests
    {
        private readonly Mock<IMongoDatabase> _databaseMock;
        private readonly Mock<IMongoCollection<SensorData>> _collectionMock;
        private readonly SensorDataRepository _repository;

        public SensorDataRepositoryTests()
        {
            _databaseMock = new Mock<IMongoDatabase>();
            _collectionMock = new Mock<IMongoCollection<SensorData>>();

            _databaseMock.Setup(db => db.GetCollection<SensorData>("SensorDataHistory", null))
                         .Returns(_collectionMock.Object);

            _repository = new SensorDataRepository(_databaseMock.Object);
        }

        [Fact]
        public async Task AddAsync_ShouldCallInsertOneAsync_WithCorrectData()
        {
            // Arrange
            var sensorData = new SensorData
            {
                Id = Guid.NewGuid(),
                FieldId = Guid.NewGuid(),
                SensorType = Domain.Enum.SensorType.Temperatura,
                Value = 25.5,
                Timestamp = DateTime.UtcNow,
                SensorDeviceId = Guid.NewGuid()
            };

            _collectionMock.Setup(c => c.InsertOneAsync(sensorData, null, default))
                           .Returns(Task.CompletedTask);

            // Act
            await _repository.AddAsync(sensorData);

            // Assert
            _collectionMock.Verify(c => c.InsertOneAsync(sensorData, null, default), Times.Once);
        }
    }
}
