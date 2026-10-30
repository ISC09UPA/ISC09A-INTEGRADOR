# ==============================================================================
# AeroPark · Script de Inicio de Servidores de Desarrollo (Backend + Frontend)
# ==============================================================================

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "🚀 Iniciando Ecosistema de Desarrollo AeroPark" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 1. Iniciar Backend .NET 10 Minimal APIs
Write-Host "⚙️  [1/2] Levantando Backend .NET 10 API en http://localhost:5000..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd Backend\EstacionamientoInteligente.API; Write-Host '--- Backend .NET 10 API (http://localhost:5000) ---' -ForegroundColor Cyan; dotnet run --urls 'http://localhost:5000'"

# 2. Iniciar Frontend Universal React Native Expo
Write-Host "📱 [2/2] Levantando Frontend React Native en http://localhost:8081..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd Frontend\mobile; Write-Host '--- Frontend Universal React Native (http://localhost:8081) ---' -ForegroundColor Cyan; npx expo start --web --port 8081"

Write-Host ""
Write-Host "====================================================" -ForegroundColor Green
Write-Host "✅ Ambos servidores han sido lanzados en terminales dedicadas:" -ForegroundColor Green
Write-Host "   • Backend API (.NET 10)  : http://localhost:5000" -ForegroundColor Green
Write-Host "   • Frontend Universal     : http://localhost:8081" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
