FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

RUN apk add --no-cache curl

ARG NEW_RELIC_AGENT_VERSION=10.26.0

RUN curl -L https://download.newrelic.com/dot_net_agent/previous_releases/${NEW_RELIC_AGENT_VERSION}/newrelic-dotnet-agent_${NEW_RELIC_AGENT_VERSION}_amd64.tar.gz | tar -C . -xz

COPY agtc-srv-analysis.sln .
COPY AgtcSrvAnalysis.Worker/*.csproj ./AgtcSrvAnalysis.Worker/
COPY AgtcSrvAnalysis.Application/*.csproj ./AgtcSrvAnalysis.Application/
COPY AgtcSrvAnalysis.Domain/*.csproj ./AgtcSrvAnalysis.Domain/
COPY AgtcSrvAnalysis.Infrastructure/*.csproj ./AgtcSrvAnalysis.Infrastructure/
COPY AgtcSrvAnalysis.Test/*.csproj ./AgtcSrvAnalysis.Test/

RUN dotnet restore

COPY . .
RUN dotnet publish AgtcSrvAnalysis.Worker/AgtcSrvAnalysis.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache icu-libs

COPY --from=build /app/newrelic-dotnet-agent /usr/local/newrelic-dotnet-agent

ENV CORECLR_ENABLE_PROFILING=1 \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    NEW_RELIC_APP_NAME="AgtcSrvAnalysis"

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN chown -R appuser:appgroup /app && chown -R appuser:appgroup /usr/local/newrelic-dotnet-agent

USER appuser

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "AgtcSrvAnalysis.Worker.dll"]