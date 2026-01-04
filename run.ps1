# Vérifie la présence du SDK .NET et restaure/build/execute le projet
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "dotnet SDK introuvable. Installez le SDK .NET depuis https://aka.ms/dotnet/download puis relancez ce script."
    exit 1
}

Write-Host "dotnet trouvé:" ($dotnet.Path)

Write-Host "Restoration des packages..."
dotnet restore
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet restore a échoué"; exit $LASTEXITCODE }

Write-Host "Build du projet..."
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet build a échoué"; exit $LASTEXITCODE }

Write-Host "Exécution du projet..."
dotnet run --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet run a échoué"; exit $LASTEXITCODE }

Write-Host "Terminé."