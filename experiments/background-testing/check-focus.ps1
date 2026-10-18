# Read-only foreground sampling during the background suite. Never activates or moves a window.
param([ValidateSet('web', 'godot', 'all')][string]$Suite = 'all')
$ErrorActionPreference = 'Stop'
$worktree = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$evidence = Join-Path $worktree ('artifacts/testing/focus-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidence | Out-Null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class BackgroundFocusProbe {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
'@
$runner = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList @('scripts/test-background.mjs', '--suite', $Suite) -WorkingDirectory $worktree -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence 'runner.log') -RedirectStandardError (Join-Path $evidence 'runner-errors.log')
$clock = [Diagnostics.Stopwatch]::StartNew()
$samples = 0
$changes = 0
$lastWindow = [IntPtr]::Zero
$violations = [Collections.Generic.List[object]]::new()
$failure = $null
try {
    while (-not $runner.HasExited) {
        if ($clock.Elapsed.TotalMinutes -gt 20) { throw 'Focus experiment timed out' }
        $window = [BackgroundFocusProbe]::GetForegroundWindow()
        $samples++
        if ($window -ne [IntPtr]::Zero -and $window -ne $lastWindow) {
            $changes++
            $lastWindow = $window
            [uint32]$foregroundProcess = 0
            [void][BackgroundFocusProbe]::GetWindowThreadProcessId($window, [ref]$foregroundProcess)
            $ancestor = $foregroundProcess
            $seen = [Collections.Generic.HashSet[uint32]]::new()
            while ($ancestor -gt 0 -and $seen.Add($ancestor)) {
                if ($ancestor -eq $runner.Id) {
                    $violations.Add(@{ elapsedSeconds = $clock.Elapsed.TotalSeconds; ownedForegroundPid = $foregroundProcess })
                    break
                }
                $row = Get-CimInstance Win32_Process -Filter "ProcessId = $ancestor"
                if (-not $row) { break }
                $ancestor = $row.ParentProcessId
            }
        }
        Start-Sleep -Milliseconds 50
        $runner.Refresh()
    }
    $runner.WaitForExit()
} catch {
    $failure = $_.Exception.Message
} finally {
    if (-not $runner.HasExited) {
        Start-Process -FilePath taskkill.exe -ArgumentList @('/PID', [string]$runner.Id, '/T', '/F') -WindowStyle Hidden -Wait
        $runner.WaitForExit()
    }
    $result = [ordered]@{
        suite = $Suite
        runnerExitCode = $runner.ExitCode
        durationSeconds = $clock.Elapsed.TotalSeconds
        samples = $samples
        observedForegroundChanges = $changes
        ownedForegroundViolations = @($violations.ToArray())
        error = $failure
        method = 'GetForegroundWindow sampled every 50ms plus ancestor lookup on changes; no focus changes or global input sent by the probe. Brief transitions between samples may be missed.'
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'focus.json') -Encoding utf8
    Write-Output "Focus evidence: $evidence"
}
if ($failure -or $runner.ExitCode -ne 0 -or $violations.Count) { exit 1 }
