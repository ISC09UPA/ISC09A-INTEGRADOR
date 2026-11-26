@echo off
REM Carga las variables de .env y corre el backend.  Uso: doble clic o  correr-local.cmd
cd /d "%~dp0"
if not exist ".env" (
  echo No existe .env. Copia .env.example a .env y llena las llaves.
  exit /b 1
)
for /f "usebackq eol=# tokens=1* delims==" %%a in (".env") do set "%%a=%%b"
dotnet run --project CiudadInteligente.API
