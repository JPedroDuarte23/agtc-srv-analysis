using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using AgtcSrvAnalysis.Domain.Enum;

namespace AgtcSrvAnalysis.Domain.Entities;

public class SensorData
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; }
    public SensorType SensorType { get; set; }
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid SensorDeviceId { get; set; }
}
