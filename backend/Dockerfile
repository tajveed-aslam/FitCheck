# Build: docker build -t fitcheck-api ./backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FitCheck.Api/FitCheck.Api.csproj FitCheck.Api/
RUN dotnet restore FitCheck.Api/FitCheck.Api.csproj
COPY FitCheck.Api/ FitCheck.Api/
RUN dotnet publish FitCheck.Api/FitCheck.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Database__MigrateOnStartup=true
# Render (and most PaaS hosts) inject PORT; default to 8080 elsewhere.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet FitCheck.Api.dll"]
