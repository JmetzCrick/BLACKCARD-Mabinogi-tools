param([Parameter(Mandatory=$true)][string]$PlanPath, [switch]$NoRestart)
$ErrorActionPreference = 'Stop'
$taskPlan = Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
$taskTarget = [IO.Path]::GetFullPath($taskPlan.Target).TrimEnd('\')
$taskStage = [IO.Path]::GetFullPath($taskPlan.Stage).TrimEnd('\')
$taskBackup = [IO.Path]::GetFullPath($taskPlan.Backup).TrimEnd('\')
if ($taskTarget -eq [IO.Path]::GetPathRoot($taskTarget).TrimEnd('\') -or !(Test-Path -LiteralPath $taskStage)) { throw 'Invalid update target or stage' }
function Resolve-UpdateChild([string]$root, [string]$relative) {
    $taskResolved = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $relative))
    if (!$taskResolved.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Update path escapes target directory' }
    return $taskResolved
}
$taskChanged = [Collections.Generic.List[object]]::new()
try {
    try { $taskParent = [Diagnostics.Process]::GetProcessById([int]$taskPlan.ProcessId) } catch { $taskParent = $null }
    if ($null -ne $taskParent -and !$taskParent.WaitForExit(60000)) { throw 'Program did not exit before update' }
    $taskFiles = Get-ChildItem -LiteralPath $taskStage -Recurse -File | Sort-Object FullName
    foreach ($taskFile in $taskFiles) {
        $taskRelative = $taskFile.FullName.Substring($taskStage.Length + 1)
        if ($taskRelative -eq 'auction-key.bin' -or $taskRelative -eq 'update-source.json') { continue }
        $taskDestination = Resolve-UpdateChild $taskTarget $taskRelative
        $taskSaved = Resolve-UpdateChild $taskBackup $taskRelative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskDestination)) -Force | Out-Null
        $taskExisted = Test-Path -LiteralPath $taskDestination
        if ($taskExisted -and (Get-Item -LiteralPath $taskDestination).PSIsContainer) { throw 'Update file conflicts with an existing directory' }
        if ($taskExisted) {
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskSaved)) -Force | Out-Null
            Copy-Item -LiteralPath $taskDestination -Destination $taskSaved -Force
        }
        $taskChanged.Add([pscustomobject]@{Destination=$taskDestination;Saved=$taskSaved;Existed=$taskExisted})
        $taskCopied = $false
        for ($taskTry = 0; $taskTry -lt 30; $taskTry++) {
            try { Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force; $taskCopied = $true; break } catch { Start-Sleep -Milliseconds 200 }
        }
        if (!$taskCopied) { throw 'Could not replace program file' }
    }
    if (!$NoRestart) { Start-Process -FilePath (Resolve-UpdateChild $taskTarget $taskPlan.Executable) -WorkingDirectory $taskTarget -WindowStyle Hidden }
} catch {
    $taskError = $_.ToString()
    for ($taskIndex = $taskChanged.Count - 1; $taskIndex -ge 0; $taskIndex--) {
        $taskChange = $taskChanged[$taskIndex]
        if (!$taskChange.Destination.StartsWith($taskTarget + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
        try {
            if ($taskChange.Existed) { Copy-Item -LiteralPath $taskChange.Saved -Destination $taskChange.Destination -Force }
            elseif (Test-Path -LiteralPath $taskChange.Destination) { Remove-Item -LiteralPath $taskChange.Destination -Force }
        } catch { $taskError += "`r`nRollback: " + $_.ToString() }
    }
    Set-Content -LiteralPath ([IO.Path]::Combine([IO.Path]::GetDirectoryName($PlanPath), 'update-error.log')) -Value $taskError -Encoding UTF8
    if (!$NoRestart) {
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show('업데이트를 적용하지 못해 이전 파일을 복원했습니다. 프로그램 폴더의 쓰기 권한을 확인하세요.', '블랙카드 도우미') | Out-Null
        Start-Process -FilePath (Resolve-UpdateChild $taskTarget $taskPlan.Executable) -WorkingDirectory $taskTarget -WindowStyle Hidden
    }
    exit 1
}

