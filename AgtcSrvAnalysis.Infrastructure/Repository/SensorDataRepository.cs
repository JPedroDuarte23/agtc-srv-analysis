using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Domain.Entities;
using MongoDB.Driver;

namespace AgtcSrvAnalysis.Infrastructure.Repository;

public class SensorDataRepository : ISensorDataRepository
{
    private readonly IMongoCollection<SensorData> _collection;

    public SensorDataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SensorData>("SensorDataHistory");
    }

    public Task AddAsync(SensorData data)
    {
        return _collection.InsertOneAsync(data);
    }
}