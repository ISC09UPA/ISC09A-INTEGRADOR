param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$backendRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $backendRoot 'InvernaderoInteligente.API'
$apiProject = Join-Path $apiDirectory 'InvernaderoInteligente.API.csproj'
$apiAssembly = Join-Path $apiDirectory 'bin\Debug\net10.0\InvernaderoInteligente.API.dll'

if (-not $SkipBuild) {
    & dotnet build $apiProject --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "La compilación de la API falló ($LASTEXITCODE)."
    }
}

if (-not (Test-Path $apiAssembly)) {
    throw "No se encontró la API compilada: $apiAssembly"
}

$id = [guid]::NewGuid().ToString('N')
$temporaryDirectory = Join-Path $env:TEMP "invernadero-backend-test-$id"
$databasePath = Join-Path $temporaryDirectory 'invernadero.db'
$stdoutPath = Join-Path $temporaryDirectory 'api.out.log'
$stderrPath = Join-Path $temporaryDirectory 'api.err.log'
$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$server = $null
$savedEnvironment = @{}
$environmentNames = @(
    'ASPNETCORE_URLS',
    'ASPNETCORE_ENVIRONMENT',
    'ConnectionStrings__Invernadero',
    'TelemetriaApi__ApiKey'
)

foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Invoke-ApiPost {
    param(
        [string]$Path,
        [string]$Json,
        [hashtable]$Headers
    )

    $request = @{
        Uri         = "$baseUrl$Path"
        Method      = 'Post'
        ContentType = 'application/json; charset=utf-8'
        Body        = [System.Text.Encoding]::UTF8.GetBytes($Json)
        UseBasicParsing = $true
    }

    if ($Headers) {
        $request.Headers = $Headers
    }

    Invoke-WebRequest @request
}

function Get-StatusCode {
    param(
        [string]$Path,
        [string]$Method = 'Get',
        [hashtable]$Headers,
        [string]$Json
    )

    try {
        if ($Method -eq 'Post') {
            $null = Invoke-ApiPost -Path $Path -Json $Json -Headers $Headers
        }
        else {
            $null = Invoke-WebRequest -Uri "$baseUrl$Path" -Method $Method -UseBasicParsing
        }

        return 200
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }

        throw
    }
}

function Assert-Equal {
    param(
        [object]$Actual,
        [object]$Expected,
        [string]$Description
    )

    if ($Actual -ne $Expected) {
        throw "$Description`: se esperaba '$Expected' y se recibió '$Actual'."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    $env:ASPNETCORE_URLS = $baseUrl
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__Invernadero = "Data Source=$databasePath"
    $env:TelemetriaApi__ApiKey = 'local-test-key'

    $server = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @($apiAssembly) `
        -WorkingDirectory $apiDirectory `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if ($server.HasExited) {
            throw "La API terminó antes de iniciar. Revisa $stderrPath."
        }

        try {
            $null = Invoke-WebRequest -Uri "$baseUrl/api/Lecturas" -UseBasicParsing -TimeoutSec 2
            $ready = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 400
        }
    }

    if (-not $ready) {
        throw "La API no respondió en $baseUrl. Revisa $stdoutPath y $stderrPath."
    }

    $apiKeyHeader = @{ 'X-Api-Key' = 'local-test-key' }
    $readingOne = '{"sensorId":"SUELO,01","tipo":"humedad_suelo","valor":37.5,"unidad":"%","invernaderoId":1,"fechaHora":"2026-11-09T12:00:00Z"}'
    $readingTwo = '{"sensorId":"SUELO-02","tipo":"temperatura","valor":22.25,"unidad":"C","invernaderoId":1,"fechaHora":"2026-11-09T12:05:00Z"}'

    Assert-Equal (Get-StatusCode -Path '/api/Lecturas' -Method Post -Json $readingOne) 401 'POST sin API key'
    Assert-Equal (Get-StatusCode -Path '/api/Lecturas' -Method Post -Headers @{ 'X-Api-Key' = 'incorrecta' } -Json $readingOne) 401 'POST con API key incorrecta'
    Write-Output 'PASS 1/10: la API rechaza una llave ausente o incorrecta (401).'

    Assert-Equal ([int](Invoke-ApiPost -Path '/api/Lecturas' -Json $readingOne -Headers $apiKeyHeader).StatusCode) 201 'Crear lectura'
    Assert-Equal ([int](Invoke-ApiPost -Path '/api/Lecturas' -Json $readingTwo -Headers $apiKeyHeader).StatusCode) 201 'Crear segunda lectura'
    Assert-Equal (Get-StatusCode -Path '/api/Lecturas' -Method Post -Headers $apiKeyHeader -Json $readingOne) 409 'Lectura duplicada'
    Write-Output 'PASS 2/10: las lecturas se guardan (201) y el duplicado responde 409.'

    $invalidReading = '{"sensorId":"SUELO-03","tipo":"humedad_suelo","unidad":"%","invernaderoId":0}'
    Assert-Equal (Get-StatusCode -Path '/api/Lecturas' -Method Post -Headers $apiKeyHeader -Json $invalidReading) 400 'DTO de lectura inválido'
    Assert-Equal (Get-StatusCode -Path '/api/Lecturas?limite=1001') 400 'Filtro de lecturas fuera de rango'
    Write-Output 'PASS 3/10: la validación del DTO y los filtros responde 400.'

    $filteredReadings = Invoke-RestMethod -Uri "$baseUrl/api/Lecturas?tipo=humedad_suelo&limite=10"
    Assert-Equal $filteredReadings.Count 1 'Cantidad de lecturas filtradas'
    Assert-Equal $filteredReadings[0].SensorId 'SUELO,01' 'Sensor del resultado filtrado'
    Assert-Equal (Get-StatusCode -Path '/api/Lecturas/9999') 404 'Lectura inexistente'
    Write-Output 'PASS 4/10: GET aplica filtros y una lectura inexistente responde 404.'

    $csv = Invoke-WebRequest `
        -Uri "$baseUrl/api/Lecturas/exportar.csv?desde=2026-11-09T11%3A59%3A00Z&hasta=2026-11-09T12%3A01%3A00Z" `
        -UseBasicParsing
    if ($csv.Headers['Content-Type'] -notlike 'text/csv*' -or
        $csv.Content -notmatch '"SUELO,01"' -or
        $csv.Content -notmatch '37\.5' -or
        $csv.Content -notmatch '2026-11-09T12:00:00\.0000000Z' -or
        $csv.Content -match 'SUELO-02') {
        throw "La exportación CSV no cumple el filtro, el escape o el formato UTC: $($csv.Content)"
    }
    Write-Output 'PASS 5/10: CSV filtra, escapa comas, conserva decimales y fechas UTC.'

    $irrigation = '{"invernaderoId":1,"zona":"Zona A","fechaHoraInicio":"2026-11-09T12:10:00Z","duracionSegundos":45,"volumenMl":250,"origen":"ia","humedadDetectada":28.5,"umbral":30}'
    $irrigationResponse = Invoke-ApiPost -Path '/api/Riegos' -Json $irrigation -Headers $apiKeyHeader
    Assert-Equal ([int]$irrigationResponse.StatusCode) 201 'Crear riego de origen IA'
    $savedIrrigation = $irrigationResponse.Content | ConvertFrom-Json
    Assert-Equal $savedIrrigation.origen 'ia' 'Origen del riego'
    Assert-Equal $savedIrrigation.volumenMl 250 'Volumen del riego'
    Write-Output 'PASS 6/10: un riego conserva el origen ia y volumenMl.'

    Assert-Equal (Get-StatusCode -Path '/api/Riegos' -Method Post -Headers $apiKeyHeader -Json $irrigation) 409 'Riego duplicado'
    Write-Output 'PASS 7/10: un riego duplicado responde 409.'

    $invalidIrrigation = '{"duracionSegundos":45,"origen":"otro"}'
    Assert-Equal (Get-StatusCode -Path '/api/Riegos' -Method Post -Headers $apiKeyHeader -Json $invalidIrrigation) 400 'DTO de riego inválido'
    Write-Output 'PASS 8/10: el origen de riego inválido responde 400.'

    $irrigations = Invoke-RestMethod -Uri "$baseUrl/api/Riegos?invernaderoId=1&limite=20"
    Assert-Equal $irrigations.Count 1 'Cantidad de riegos consultados'
    Assert-Equal $irrigations[0].Origen 'ia' 'Origen del riego consultado'
    Write-Output 'PASS 9/10: GET consulta los riegos guardados.'

    $allReadings = Invoke-RestMethod -Uri "$baseUrl/api/Lecturas"
    if ($allReadings.Count -ne 2) {
        throw "Se esperaban dos lecturas guardadas; se recibieron $($allReadings.Count)."
    }
    Write-Output 'PASS 10/10: GET sigue disponible sin API key y devuelve los datos persistidos.'
}
finally {
    if ($server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force
    }

    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }

    if (Test-Path $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
