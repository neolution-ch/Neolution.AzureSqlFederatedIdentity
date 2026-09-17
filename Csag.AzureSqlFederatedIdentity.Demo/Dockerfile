# Build context is the repository root:
#   docker build -f Csag.AzureSqlFederatedIdentity.Demo/Dockerfile -t csag-demo .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project and lock files first, so the restore layer is reused until a dependency changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY Csag.AzureSqlFederatedIdentity/Csag.AzureSqlFederatedIdentity.csproj Csag.AzureSqlFederatedIdentity/packages.lock.json Csag.AzureSqlFederatedIdentity/
COPY Csag.AzureSqlFederatedIdentity.Demo/Csag.AzureSqlFederatedIdentity.Demo.csproj Csag.AzureSqlFederatedIdentity.Demo/packages.lock.json Csag.AzureSqlFederatedIdentity.Demo/
RUN dotnet restore Csag.AzureSqlFederatedIdentity.Demo --locked-mode

COPY . .
RUN dotnet publish Csag.AzureSqlFederatedIdentity.Demo -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Cloud Run sends requests to the container on $PORT, which defaults to 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root user shipped with the aspnet image.
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Csag.AzureSqlFederatedIdentity.Demo.dll"]
