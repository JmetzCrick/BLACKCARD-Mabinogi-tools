$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
[xml]$taskProject = Get-Content BuffAssistant/BuffAssistant.csproj
$taskVersion = [string]$taskProject.Project.PropertyGroup.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+(-beta\.\d+)?$') { throw 'Invalid release version' }
$taskPublish = Join-Path $taskRoot ('Publish/GitHub-' + $taskVersion)
dotnet publish BuffAssistant/BuffAssistant.csproj -c Release -r win-x64 --self-contained true -p:PublicRelease=true -o $taskPublish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
if (Test-Path -LiteralPath (Join-Path $taskPublish 'auction-key.bin')) { throw 'Account-bound private key must not be included' }
foreach ($taskFile in @('github-update.json', 'coreclr.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $taskPublish $taskFile))) { throw ('Missing distribution file: ' + $taskFile) }
}
Copy-Item -Path DistributionDocs/* -Destination $taskPublish -Force
$taskArtifacts = Join-Path $taskRoot 'ReleaseArtifacts'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskZip = Join-Path $taskArtifacts ('BlackCardHelper-' + $taskVersion + '-win-x64.zip')
Compress-Archive -Path (Join-Path $taskPublish '*') -DestinationPath $taskZip -CompressionLevel Optimal -Force
$taskHash = (Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($taskZip + '.sha256') -Value ($taskHash + '  ' + [IO.Path]::GetFileName($taskZip)) -Encoding ascii
Write-Output $taskZip
