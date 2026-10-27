@echo off
:: Solicitar permisos de administrador si no se tienen
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Solicitando permisos de administrador...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

title Instalador de Certificado - Qubi Cost Electrical
echo =======================================================================
echo   Instalador de Certificado de Confianza Authenticode - Qubi Cost
echo =======================================================================
echo.
echo Agregando certificado publico a los almacenes de confianza de Windows...
echo.

certutil -addstore -f "TrustedPublisher" "%~dp0QubiCostElectrical-Public.cer"
certutil -addstore -f "Root" "%~dp0QubiCostElectrical-Public.cer"

echo.
echo =======================================================================
echo   Certificado instalado exitosamente.
echo   Ahora Windows reconocera a Qubi Cost Electrical como Editor Verificado.
echo =======================================================================
echo.
pause
