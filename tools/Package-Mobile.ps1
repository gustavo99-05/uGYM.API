param([Parameter(Mandatory=$true)][string]$FrontendPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = (Resolve-Path -LiteralPath $FrontendPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'mobile\index.html'))) { throw 'Frontend mobile não encontrado.' }
$output = Join-Path $projectRoot 'artifacts\package'
& dotnet publish (Join-Path $projectRoot 'uGYM.API.csproj') --configuration Release --output $output
if ($LASTEXITCODE -ne 0) { throw 'Falha no dotnet publish.' }
$wwwroot = Join-Path $output 'wwwroot'
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
foreach ($file in @('index.html','script.js','style.css','config.js','api-client.js','mascote.png')) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination $wwwroot -Force
}
Copy-Item -LiteralPath (Join-Path $source 'mobile') -Destination $wwwroot -Recurse -Force
Write-Host "Pacote local criado em $output. Não houve publicação nem alteração no banco."
