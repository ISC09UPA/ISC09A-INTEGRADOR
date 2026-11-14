# Se construye desde la raíz del repo: docker build -f src/AlmacenInteligente.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/AlmacenInteligente.Domain/*.csproj src/AlmacenInteligente.Domain/
COPY src/AlmacenInteligente.Application/*.csproj src/AlmacenInteligente.Application/
COPY src/AlmacenInteligente.Infrastructure/*.csproj src/AlmacenInteligente.Infrastructure/
COPY src/AlmacenInteligente.Api/*.csproj src/AlmacenInteligente.Api/
RUN dotnet restore src/AlmacenInteligente.Api/AlmacenInteligente.Api.csproj
COPY src/ src/
RUN dotnet publish src/AlmacenInteligente.Api/AlmacenInteligente.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AlmacenInteligente.Api.dll"]
