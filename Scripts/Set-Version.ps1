param([Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+(-beta\.\d+)?$')][string]$Version)
$ErrorActionPreference = 'Stop'
$taskProjectPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'BuffAssistant/BuffAssistant.csproj'
$taskNumber = $Version.Split('-')[0]
$taskDisplay = if ($Version.Contains('-beta')) { 'BETA ' + $taskNumber } else { $taskNumber }
$taskText = Get-Content -LiteralPath $taskProjectPath -Raw -Encoding UTF8
foreach ($taskProperty in @{Version=$Version;AssemblyVersion=($taskNumber+'.0');FileVersion=($taskNumber+'.0');InformationalVersion=$taskDisplay}.GetEnumerator()) {
    $taskPattern = '<' + $taskProperty.Key + '>[^<]*</' + $taskProperty.Key + '>'
    $taskReplacement = '<' + $taskProperty.Key + '>' + $taskProperty.Value + '</' + $taskProperty.Key + '>'
    $taskText = [regex]::Replace($taskText, $taskPattern, $taskReplacement)
}
Set-Content -LiteralPath $taskProjectPath -Value $taskText -Encoding utf8
Write-Output ('Version updated: ' + $Version + '. Commit and push to main to publish automatically.')

