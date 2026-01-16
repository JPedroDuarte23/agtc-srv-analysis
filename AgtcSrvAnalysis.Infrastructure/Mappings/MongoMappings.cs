using System.Diagnostics.CodeAnalysis;
using AgtcSrvAnalysis.Domain.Entities;
using AgtcSrvAnalysis.Domain.Enum;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace FiapSrvPayment.Infrastructure.Mappings;

[ExcludeFromCodeCoverage]
public static class MongoMappings
{
    public static void ConfigureMappings()
    {
        BsonClassMap.RegisterClassMap<SensorData>(map =>
        {
            map.AutoMap();

            map.SetIgnoreExtraElements(true);

            map.MapIdMember(x => x.Id)
                .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

            map.MapMember(x => x.FieldId)
                .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

            map.MapMember(x => x.SensorDeviceId)
                .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

            map.MapMember(x => x.SensorType)
                .SetSerializer(new EnumSerializer<SensorType>(BsonType.String));
        });
    }
}