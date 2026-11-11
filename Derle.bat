@echo off
chcp 65001 >nul
echo CatNur derleniyor, lutfen bekleyin...
cd /d "%~dp0"
taskkill /f /im CatNur.exe >nul 2>&1
dotnet publish src\CatNur\CatNur.csproj -c Release -o "%~dp0CatNur"
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ====================================
    echo   DERLEME BASARIYLA TAMAMLANDI!
    echo ====================================
    start "" "%~dp0CatNur\CatNur.exe"
) else (
    echo.
    echo [HATA] Derleme sirasinda bir sorun olustu.
)
timeout /t 3 >nul
