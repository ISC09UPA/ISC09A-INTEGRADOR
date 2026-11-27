#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../Backend"
echo "=== Building Identity ==="
dotnet build Identity/CasaDomotica.Identity.sln
echo "=== Testing Identity ==="
dotnet test Identity/CasaDomotica.Identity.sln --no-build
echo "=== Building Home ==="
dotnet build Home/CasaDomotica.Home.sln
echo "=== Testing Home ==="
dotnet test Home/CasaDomotica.Home.sln --no-build
echo "=== Building Home.Functions (no active triggers) ==="
dotnet build Home.Functions/CasaDomotica.Home.Functions.csproj
echo "=== Backend tests complete ==="
