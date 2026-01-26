using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Domain.Entities;
using Prometheus;

namespace AgtcSrvAnalysis.Application.Services;

public class TelemetryAnalyzer : ITelemetryAnalyzer
{
    private readonly ISensorDataRepository _sensorDataRepository;

    private static readonly string[] StandardLabels = new[]
    {
        "farmer_name",
        "property_name",
        "field_name",
        "field_id"
    };

    private static readonly Counter EventsProcessed = Metrics
        .CreateCounter("agro_events_processed_total", "Total de eventos de sensores processados");


    private static readonly Gauge SoilHumidity = Metrics
        .CreateGauge("agro_soil_humidity_percent", "Umidade atual do solo (%)",
            new GaugeConfiguration { LabelNames = StandardLabels });

    private static readonly Gauge AmbientTemperature = Metrics
        .CreateGauge("agro_temperature_celsius", "Temperatura ambiente atual (°C)",
            new GaugeConfiguration { LabelNames = StandardLabels });

    private static readonly Gauge AtmosphericPressure = Metrics
        .CreateGauge("agro_pressure_hpa", "Pressão atmosférica atual (hPa)",
            new GaugeConfiguration { LabelNames = StandardLabels });

    private static readonly Counter DroughtAlerts = Metrics
        .CreateCounter("agro_alert_drought_total", "Total de alertas de seca (Umidade < 30%)",
            new CounterConfiguration { LabelNames = StandardLabels });

    private static readonly Counter FrostAlerts = Metrics
        .CreateCounter("agro_alert_frost_total", "Total de alertas de geada (Temp < 5°C)",
            new CounterConfiguration { LabelNames = StandardLabels });

    private static readonly Counter HeatAlerts = Metrics
        .CreateCounter("agro_alert_heat_total", "Total de alertas de calor excessivo (Temp > 35°C)",
            new CounterConfiguration { LabelNames = StandardLabels });

    private static readonly Counter StormAlerts = Metrics
        .CreateCounter("agro_alert_storm_total", "Total de alertas de tempestade (Pressão < 1000 hPa)",
            new CounterConfiguration { LabelNames = StandardLabels });


    public TelemetryAnalyzer(ISensorDataRepository repository)
    {
        _sensorDataRepository = repository;
    }

    public async Task AnalyzeAndPersistAsync(SensorData data)
    {
        // 1. Persistência no Mongo (Dados brutos)
        await _sensorDataRepository.AddAsync(data);

        // 2. Incrementa heartbeat do sistema
        EventsProcessed.Inc();

        // Normaliza o tipo para evitar erros de Case Sensitive
        var sensorType = data.SensorType.ToString().ToLowerInvariant();

        switch (sensorType)
        {
            case "umidade":
                ProcessHumidity(data);
                break;

            case "temperatura":
                ProcessTemperature(data);
                break;

            case "pressao":
                ProcessPressure(data);
                break;

            default:
                Console.WriteLine($"[AVISO] Tipo de sensor desconhecido: {data.SensorType}");
                break;
        }
    }

    private void ProcessHumidity(SensorData data)
    {
        SoilHumidity
            .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
            .Set(data.Value);

        if (data.Value < 30.0)
        {
            DroughtAlerts
                .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
                .Inc();

            LogAlert("SECA 🌵", data, $"{data.Value}%");
        }
    }

    private void ProcessTemperature(SensorData data)
    {
        AmbientTemperature
            .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
            .Set(data.Value);

        if (data.Value < 5.0)
        {
            FrostAlerts
                .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
                .Inc();

            LogAlert("GEADA ❄️", data, $"{data.Value}°C");
        }
        else if (data.Value > 35.0)
        {
            HeatAlerts
                .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
                .Inc();

            LogAlert("CALOR EXCESSIVO 🔥", data, $"{data.Value}°C");
        }
    }

    private void ProcessPressure(SensorData data)
    {
        AtmosphericPressure
            .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
            .Set(data.Value);

        if (data.Value < 1000.0)
        {
            StormAlerts
                .WithLabels(data.farmerName, data.propertyName, data.fieldName, data.FieldId.ToString())
                .Inc();

            LogAlert("BAIXA PRESSÃO / TEMPESTADE ⛈️", data, $"{data.Value} hPa");
        }
    }

    private void LogAlert(string alertType, SensorData data, string valueFormatted)
    {
        Console.WriteLine($"[ALERTA 🚨] {alertType} | Fazenda: {data.propertyName} | Talhão: {data.fieldName} ({data.FieldId}) | Valor: {valueFormatted}");
    }
}