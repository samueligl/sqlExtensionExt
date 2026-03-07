$ErrorActionPreference = "Continue"

$realExtensions = "$env:LOCALAPPDATA\Microsoft\SSMS\22.0_b279a7bf\Extensions"
$expExtensions  = "$env:LOCALAPPDATA\Microsoft\SSMS\22.0_b279a7bfExp\Extensions"

$realCache = "$env:LOCALAPPDATA\Microsoft\SSMS\22.0_b279a7bf\ComponentModelCache"
$expCache  = "$env:LOCALAPPDATA\Microsoft\SSMS\22.0_b279a7bfExp\ComponentModelCache"

$roots = @($realExtensions, $expExtensions)
$caches = @($realCache, $expCache)

Write-Host "Cerrando SSMS si está abierto..." -ForegroundColor Cyan
Get-Process ssms -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "`nBuscando instalaciones previas de PowerSql..." -ForegroundColor Cyan

foreach ($root in $roots) {
    if (Test-Path $root) {
        Write-Host "Revisando: $root" -ForegroundColor Yellow

        $matches = Get-ChildItem -Path $root -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -match 'PowerSql' -or $_.FullName -match 'PowerSql'
            }

        if ($matches) {
            foreach ($item in ($matches | Sort-Object FullName -Descending)) {
                try {
                    Remove-Item $item.FullName -Recurse -Force -ErrorAction Stop
                    Write-Host "Eliminado: $($item.FullName)" -ForegroundColor Green
                }
                catch {
                    Write-Host "No se pudo eliminar: $($item.FullName)" -ForegroundColor Red
                    Write-Host $_.Exception.Message -ForegroundColor DarkRed
                }
            }
        }
        else {
            Write-Host "No se encontró PowerSql en $root" -ForegroundColor DarkYellow
        }
    }
    else {
        Write-Host "Ruta no encontrada: $root" -ForegroundColor DarkYellow
    }
}

Write-Host "`nLimpiando caché..." -ForegroundColor Cyan

foreach ($cache in $caches) {
    if (Test-Path $cache) {
        try {
            Remove-Item $cache -Recurse -Force -ErrorAction Stop
            Write-Host "Cache eliminada: $cache" -ForegroundColor Green
        }
        catch {
            Write-Host "No se pudo eliminar cache: $cache" -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor DarkRed
        }
    }
    else {
        Write-Host "Cache no encontrada: $cache" -ForegroundColor DarkYellow
    }
}

Write-Host "`nProceso de limpieza finalizado." -ForegroundColor Cyan
