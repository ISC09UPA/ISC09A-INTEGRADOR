# Construir desde la carpeta Backend:  docker build -t viaclara-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CiudadInteligente.Domain/*.csproj CiudadInteligente.Domain/
COPY CiudadInteligente.Application/*.csproj CiudadInteligente.Application/
COPY CiudadInteligente.Infrastructure/*.csproj CiudadInteligente.Infrastructure/
COPY CiudadInteligente.API/*.csproj CiudadInteligente.API/
RUN dotnet restore CiudadInteligente.API/CiudadInteligente.API.csproj
COPY . .
RUN dotnet publish CiudadInteligente.API/CiudadInteligente.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Esp32__Transporte=IoTHub
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CiudadInteligente.API.dll"]
