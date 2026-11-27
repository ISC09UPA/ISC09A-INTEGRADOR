#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../Backend/Identity"
dotnet build src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj
dotnet run --project src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj --urls "http://localhost:5001"
