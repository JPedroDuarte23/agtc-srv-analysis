# 🧠 Agro.Analysis.Worker (Processor & Alerts)

Serviço de background (Worker Service) que processa os dados de telemetria de forma assíncrona.

## 📋 Responsabilidades
1. **Consumo:** Lê mensagens da fila AWS SQS (`sensor-data-queue`).
2. **Persistência:** Salva o histórico de leitura no MongoDB (Time Series).
3. **Análise:** Verifica regras de negócio (Ex: Umidade < 30%).
4. **Observabilidade:** Expõe métricas de negócio para o Prometheus.

## 📊 Métricas Expostas (Prometheus)
- `agro_sensor_humidity_value` (Gauge): Valor atual da umidade por TalhãoId.
- `agro_alert_triggered_total` (Counter): Contador de alertas disparados.

## 🛠️ Stack Tecnológica
- .NET 8 Worker Service
- AWS SDK (SQS)
- MongoDB
- Prometheus-net

## ⚙️ Configuração
```json
{
  "AWS": {
    "SqsQueueUrl": "https://sqs.us-east-1.amazonaws.com/123456/sensor-data-queue"
  },
  "Thresholds": {
    "HumidityMin": 30.0
  }
}