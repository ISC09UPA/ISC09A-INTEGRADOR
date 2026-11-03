# Se construye desde la raíz del repo: docker build -f src/AlmacenInteligente.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/AlmacenInteligente.Domain/*.csproj src/AlmacenInteligente.Domain/
COPY src/AlmacenInteligente.Application/*.csproj src/AlmacenInteligente.Application/
COPY src/AlmacenInteligente.Infrastructure/*.csproj src/AlmacenInteligente.Infrastructure/
COPY src/AlmacenInteligente.Web/*.csproj src/AlmacenInteligente.Web/
RUN dotnet restore src/AlmacenInteligente.Web/AlmacenInteligente.Web.csproj
COPY src/ src/
RUN dotnet publish src/AlmacenInteligente.Web/AlmacenInteligente.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Llaves de Data Protection (cookies de sesión y antiforgery) en un volumen, para que sobrevivan al recrear el contenedor.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID /home/app/.aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "AlmacenInteligente.Web.dll"]
