@echo off
title AeroPark Development Launcher
echo ====================================================
echo 🚀 Iniciando Ecosistema de Desarrollo AeroPark
echo ====================================================

echo.
echo ⚙️  [1/2] Levantando Backend .NET 10 API en http://localhost:5000...
start "AeroPark Backend API (.NET 10)" powershell -NoExit -Command "cd Backend\EstacionamientoInteligente.API; Write-Host '--- Backend .NET 10 API (http://localhost:5000) ---' -ForegroundColor Cyan; dotnet run --urls 'http://localhost:5000'"

echo 📱 [2/2] Levantando Frontend React Native en http://localhost:8081...
start "AeroPark Frontend (React Native)" powershell -NoExit -Command "cd Frontend\mobile; Write-Host '--- Frontend Universal React Native (http://localhost:8081) ---' -ForegroundColor Cyan; npx expo start --web --port 8081"

echo.
echo ====================================================
echo ✅ Ambos servidores han sido iniciados.
echo    • Backend API (.NET 10)  : http://localhost:5000
echo    • Frontend Universal     : http://localhost:8081
echo ====================================================
echo.
pause
