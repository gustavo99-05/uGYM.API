param(
    [string]$FrontendPath = $env:UGYM_FRONTEND_PATH,
    [string]$LanIp,
    [string]$CertificatePath,
    [string]$KeyPath,
    [int]$Port = 5195,
    [switch]$AllowInsecureTest
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $FrontendPath) {
    $FrontendPath = Join-Path $env:USERPROFILE 'OneDrive\Documentos\pasta de conteudos academicos !\WebApplication1\front end'
}
$FrontendPath = (Resolve-Path -LiteralPath $FrontendPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $FrontendPath 'mobile\index.html'))) { throw 'Pasta frontend inválida.' }
if (-not $CertificatePath -and -not $AllowInsecureTest) { throw 'Informe certificado HTTPS ou use -AllowInsecureTest exclusivamente com dados fictícios.' }
if ($LanIp) {
    $parsed = [System.Net.IPAddress]::Parse($LanIp)
    $bytes = $parsed.GetAddressBytes()
    $private = $bytes.Length -eq 4 -and ($bytes[0] -eq 10 -or ($bytes[0] -eq 192 -and $bytes[1] -eq 168) -or ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31))
    if (-not $private) { throw 'Informe somente o IPv4 privado da rede local, obtido pelo ipconfig.' }
    if (-not $CertificatePath -and -not $AllowInsecureTest) { throw 'Para uso no celular, informe certificado HTTPS. HTTP requer -AllowInsecureTest e somente dados fictícios em rede isolada.' }
}
$names = @('ASPNETCORE_ENVIRONMENT','Frontend__Path','Security__AllowInsecureLocalTesting','Kestrel__Certificates__Default__Path','Kestrel__Certificates__Default__KeyPath')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Frontend__Path = $FrontendPath
    $env:Security__AllowInsecureLocalTesting = if ($AllowInsecureTest) { 'true' } else { 'false' }
    $protocol = 'http'
    if ($CertificatePath) {
        if (-not $KeyPath) { throw 'Informe -KeyPath com a chave PEM do certificado.' }
        $env:Kestrel__Certificates__Default__Path = (Resolve-Path -LiteralPath $CertificatePath).Path
        $env:Kestrel__Certificates__Default__KeyPath = (Resolve-Path -LiteralPath $KeyPath).Path
        $protocol = 'https'
    }
    $address = if ($LanIp) { $LanIp } else { 'localhost' }
    $baseUrl = '{0}://{1}:{2}' -f $protocol,$address,$Port
    Write-Host "Mobile: $baseUrl/mobile/"
    Write-Host "Desktop: $baseUrl/"
    Write-Host 'Nenhuma migration é aplicada por este script. Ctrl+C encerra o servidor.'
    & dotnet run --no-launch-profile --project (Join-Path $projectRoot 'uGYM.API.csproj') --urls $baseUrl
    if ($LASTEXITCODE -ne 0) { throw 'A API encerrou com erro.' }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
