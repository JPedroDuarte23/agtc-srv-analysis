using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgtcSrvAnalysis.Domain.Entities;

namespace AgtcSrvAnalysis.Application.Interfaces;

public interface ITelemetryAnalyzer
{
    Task AnalyzeAndPersistAsync(SensorData data);
}
