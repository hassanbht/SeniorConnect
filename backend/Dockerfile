# Multi-stage production Dockerfile for SeniorConnect .NET 10 Modular Monolith API
# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY ["SeniorConnect.slnx", "./"]
COPY ["SeniorConnect.Domain/SeniorConnect.Domain.csproj", "SeniorConnect.Domain/"]
COPY ["SeniorConnect.Infrastructure/SeniorConnect.Infrastructure.csproj", "SeniorConnect.Infrastructure/"]
COPY ["SeniorConnect.Api/SeniorConnect.Api.csproj", "SeniorConnect.Api/"]
COPY ["modules/Identity/SeniorConnect.Modules.Identity.csproj", "modules/Identity/"]
COPY ["modules/Profiles/SeniorConnect.Modules.Profiles.csproj", "modules/Profiles/"]
COPY ["modules/Organizations/SeniorConnect.Modules.Organizations.csproj", "modules/Organizations/"]
COPY ["modules/HelpRequests/SeniorConnect.Modules.HelpRequests.csproj", "modules/HelpRequests/"]
COPY ["modules/TrustSafety/SeniorConnect.Modules.TrustSafety.csproj", "modules/TrustSafety/"]
COPY ["modules/Community/SeniorConnect.Modules.Community.csproj", "modules/Community/"]
COPY ["modules/Family/SeniorConnect.Modules.Family.csproj", "modules/Family/"]
COPY ["modules/Notifications/SeniorConnect.Modules.Notifications.csproj", "modules/Notifications/"]
COPY ["modules/Matching/SeniorConnect.Modules.Matching.csproj", "modules/Matching/"]
COPY ["modules/Reporting/SeniorConnect.Modules.Reporting.csproj", "modules/Reporting/"]

RUN dotnet restore "SeniorConnect.Api/SeniorConnect.Api.csproj"

# Copy source code and build release
COPY . .
WORKDIR "/src/SeniorConnect.Api"
RUN dotnet publish "SeniorConnect.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime (Chiseled / Non-Root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SeniorConnect.Api.dll"]
