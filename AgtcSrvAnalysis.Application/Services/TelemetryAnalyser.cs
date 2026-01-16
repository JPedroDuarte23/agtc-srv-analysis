using AgtcSrvAnalysis.Application.Interfaces;
using AgtcSrvAnalysis.Domain.Entities;
using Prometheus;

namespace AgtcSrvAnalysis.Application.Services;

public class TelemetryAnalyzer : ITelemetryAnalyzer
{
    private readonly ISensorDataRepository _repository;

    // =================================================================
    // MÉTRICAS PROMETHEUS (DEFINIÇÃO)
    // =================================================================

    // Contador geral de mensagens
    private static readonly Counter EventsProcessed = Metrics
        .CreateCounter("agro_events_processed_total", "Total de eventos de sensores processados");

    // --- UMIDADE ---
    private static readonly Gauge SoilHumidity = Metrics
        .CreateGauge("agro_soil_humidity_percent", "Umidade atual do solo (%)", new[] { "field_id" });

    private static readonly Counter DroughtAlerts = Metrics
        .CreateCounter("agro_alert_drought_total", "Total de alertas de seca (Umidade < 30%)");

    // --- TEMPERATURA ---
    private static readonly Gauge AmbientTemperature = Metrics
        .CreateGauge("agro_temperature_celsius", "Temperatura ambiente atual (°C)", new[] { "field_id" });

    private static readonly Counter FrostAlerts = Metrics
        .CreateCounter("agro_alert_frost_total", "Total de alertas de geada (Temp < 5°C)");

    private static readonly Counter HeatAlerts = Metrics
        .CreateCounter("agro_alert_heat_total", "Total de alertas de calor excessivo (Temp > 35°C)");

    // --- PRESSÃO ---
    private static readonly Gauge AtmosphericPressure = Metrics
        .CreateGauge("agro_pressure_hpa", "Pressão atmosférica atual (hPa)", new[] { "field_id" });

    private static readonly Counter StormAlerts = Metrics
        .CreateCounter("agro_alert_storm_total", "Total de alertas de tempestade (Pressão < 1000 hPa)");


    public TelemetryAnalyzer(ISensorDataRepository repository)
    {
        _repository = repository;
    }

    public async Task AnalyzeAndPersistAsync(SensorData data)
    {
        // 1. Persistir no Histórico (MongoDB) - Salva tudo, independente de ser alerta
        await _repository.AddAsync(data);

        // 2. Atualizar Métrica Geral
        EventsProcessed.Inc();

        // 3. Roteamento da Lógica de Negócio
        // Normalizamos para evitar problemas com "Temperatura" vs "temperatura"
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

    // =================================================================
    // REGRAS DE NEGÓCIO ESPECÍFICAS
    // =================================================================

    private void ProcessHumidity(SensorData data)
    {
        // Atualiza Grafana (Gauge)
        SoilHumidity.WithLabels(data.FieldId.ToString()).Set(data.Value);

        // Regra: Seca Crítica (< 30%)
        if (data.Value < 30.0)
        {
            DroughtAlerts.Inc();
            LogAlert("SECA", data, $"{data.Value}%");
        }
    }

    private void ProcessTemperature(SensorData data)
    {
        // Atualiza Grafana (Gauge)
        AmbientTemperature.WithLabels(data.FieldId.ToString()).Set(data.Value);

        // Regra 1: Risco de Geada (< 5°C)
        if (data.Value < 5.0)
        {
            FrostAlerts.Inc();
            LogAlert("GEADA", data, $"{data.Value}°C");
        }
        // Regra 2: Estresse Térmico (> 35°C)
        else if (data.Value > 35.0)
        {
            HeatAlerts.Inc();
            LogAlert("CALOR EXCESSIVO", data, $"{data.Value}°C");
        }
    }

    private void ProcessPressure(SensorData data)
    {
        // Atualiza Grafana (Gauge)
        AtmosphericPressure.WithLabels(data.FieldId.ToString()).Set(data.Value);

        // Regra: Queda de pressão indica tempestade (< 1000 hPa)
        // Nota: Pressão normal ao nível do mar é ~1013 hPa
        if (data.Value < 1000.0)
        {
            StormAlerts.Inc();
            LogAlert("TEMPESTADE/BAIXA PRESSÃO", data, $"{data.Value} hPa");
        }
    }

    private void LogAlert(string alertType, SensorData data, string valueFormatted)
    {
        // Dica: Esse log vai aparecer no Console do Docker/Kubernetes
        Console.WriteLine($"[ALERTA 🚨] {alertType} detectada no Talhão {data.FieldId} | Valor: {valueFormatted}");
    }
}