#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../Backend/Home"
dotnet build src/CasaDomotica.Home.Api/CasaDomotica.Home.Api.csproj
dotnet run --project src/CasaDomotica.Home.Api/CasaDomotica.Home.Api.csproj --urls "http://localhost:5002"
