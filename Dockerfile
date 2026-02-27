FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CompraProgramada.sln .
COPY src/CompraProgramada.Domain/CompraProgramada.Domain.csproj src/CompraProgramada.Domain/
COPY src/CompraProgramada.Application/CompraProgramada.Application.csproj src/CompraProgramada.Application/
COPY src/CompraProgramada.Infrastructure/CompraProgramada.Infrastructure.csproj src/CompraProgramada.Infrastructure/
COPY src/CompraProgramada.API/CompraProgramada.API.csproj src/CompraProgramada.API/
COPY tests/CompraProgramada.Tests/CompraProgramada.Tests.csproj tests/CompraProgramada.Tests/

RUN dotnet restore

COPY . .

RUN dotnet publish src/CompraProgramada.API/CompraProgramada.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY cotacoes/ /app/cotacoes/

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "CompraProgramada.API.dll"]
