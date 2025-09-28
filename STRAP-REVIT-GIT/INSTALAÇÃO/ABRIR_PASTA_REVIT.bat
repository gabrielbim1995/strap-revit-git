@echo off
echo.
echo ========================================
echo  ABRINDO PASTA DE ADD-INS DO REVIT 2024
echo ========================================
echo.
echo Pasta de destino: %APPDATA%\Autodesk\Revit\Addins\2024
echo.
echo Pressione qualquer tecla para abrir a pasta...
pause >nul

:: Criar pasta se não existir
if not exist "%APPDATA%\Autodesk\Revit\Addins\2024" (
    echo Criando pasta de add-ins...
    mkdir "%APPDATA%\Autodesk\Revit\Addins\2024"
)

:: Abrir pasta
explorer "%APPDATA%\Autodesk\Revit\Addins\2024"

echo.
echo ========================================
echo  PASTA ABERTA!
echo ========================================
echo.
echo AGORA:
echo 1. Copie StrapRevit.dll para esta pasta
echo 2. Copie STRAP-REVIT.addin para esta pasta  
echo 3. Reinicie o Revit
echo 4. Procure a aba "STRAP-REVIT" no Ribbon
echo.
echo Pressione qualquer tecla para fechar...
pause >nul

