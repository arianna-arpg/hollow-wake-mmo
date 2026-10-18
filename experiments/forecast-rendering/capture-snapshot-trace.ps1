param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedHead,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('ManagedOnly','WithScheduler')][string]$Mode = 'ManagedOnly',
    [switch]$Run
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7 for bounded owned-process cleanup' }
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $taskRoot
if ((git rev-parse HEAD).Trim() -ne $ExpectedHead) { throw 'Source differs from the agreed validated head' }
if (git status --porcelain=v1) { throw 'Commit source and review evidence before capture' }
$out = [IO.Path]::GetFullPath((Join-Path $taskRoot $OutputDirectory))
$ignored = [IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$out.StartsWith($ignored, [StringComparison]::OrdinalIgnoreCase)) { throw 'Raw traces must remain under this worktree artifacts directory' }
if (Test-Path -LiteralPath $out) { throw 'Preserve previous evidence; output directory must be new' }
$tracer = Join-Path $taskRoot 'artifacts/diagnostics/dotnet-trace/dotnet-trace.exe'
if (!(Test-Path -LiteralPath $tracer)) { throw 'Install the pinned isolated tool described in the trace plan first' }
$version = (& $tracer --version) -join ''
if ($LASTEXITCODE -ne 0 -or !$version.StartsWith('10.0.750501+')) { throw 'Unexpected tracing tool version' }
$dll = Join-Path $taskRoot 'src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Build the agreed clean head in Release first' }
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
if (!$productVersion.EndsWith('+' + $ExpectedHead)) { throw 'The Release executable was not built from the agreed clean head' }
$profile = Join-Path $PSScriptRoot 'snapshot-scheduling.wprp'
$sessionName = 'HollowWakeSnapshot-' + [Guid]::NewGuid().ToString('N')
if ($Mode -eq 'WithScheduler') {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Windows scheduling capture requires an elevated session; no workload was started' }
    $status = (& wpr -status) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $status -notmatch 'WPR is not recording') { throw 'Existing or unknown WPR state; no recorder is modified' }
    & wpr -profiles $profile | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Windows capture profile was rejected' }
}
if (!$Run) {
    Write-Output "Preflight passed for $ExpectedHead ($Mode). Nothing recorded. Add -Run only in the agreed exclusive window."
    exit 0
}

New-Item -ItemType Directory -Path $out | Out-Null
[Diagnostics.Process]::GetCurrentProcess().PriorityClass = 'Normal'
$trace = Join-Path $out 'managed.nettrace'
$frames = Join-Path $out 'frames.json'
$scheduler = Join-Path $out 'scheduler.etl'
$clr = 'gc+assemblyloader+loader+jit+contention+exception+threading+jittedmethodiltonativemap+compilation+waithandle'
$arguments = @('collect','--profile','dotnet-sampled-thread-time','--clrevents',$clr,'--clreventlevel','verbose',
    '--providers','HollowWake-SnapshotProfile:0xFFFFFFFFFFFFFFFF:5','--buffersize','64','--duration','00:00:00:30',
    '--output',$trace,'--show-child-io','--','dotnet','exec',$dll,'bench','--only','tick','--snapshot-profile',$frames,'--json')
$environment = [ordered]@{
    LocalOnly = $true; SourceHead = $ExpectedHead; Mode = $Mode; Utc = [DateTime]::UtcNow.ToString('o')
    Priority = 'Normal'; TracerVersion = $version; TracerSha256 = (Get-FileHash -LiteralPath $tracer -Algorithm SHA256).Hash
    ProductVersion = $productVersion; ExecutableSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    Runtime = (& dotnet --version); Arguments = $arguments; Worktree = $taskRoot
    Scope = 'One instrumented default tick workload; fixed 30s trace duration, 45s process watchdog, no retry. Not budget evidence.'
    SchedulerLimit = 'Managed thread-time samples cannot establish OS scheduling. WithScheduler records bounded machine-wide scheduler metadata locally and filters analysis to the owned benchmark.'
}
$environment | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'environment.json')
$wprStarted = $false
$process = $null
$exitCode = -1
$cleanupFailures = [Collections.Generic.List[string]]::new()
try {
    if ($Mode -eq 'WithScheduler') {
        & wpr -start ($profile + '!SnapshotScheduling.Light') -instancename $sessionName > (Join-Path $out 'wpr-start.txt')
        if ($LASTEXITCODE -ne 0) { throw 'WPR start failed; workload was not started' }
        $wprStarted = $true
    }
    $start = [Diagnostics.ProcessStartInfo]::new($tracer)
    $start.WorkingDirectory = $taskRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $finished = $process.WaitForExit(45000)
    if (!$finished) { $process.Kill($true); $process.WaitForExit() }
    [IO.File]::WriteAllText((Join-Path $out 'console.txt'), $stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $out 'stderr.txt'), $stderr.GetAwaiter().GetResult())
    $exitCode = $process.ExitCode
    if (!$finished) { throw 'Capture watchdog expired; partial evidence retained, no retry' }
    if ($exitCode -ne 0) { throw 'Capture failed; preserve its first output without retry' }
} finally {
    try {
        if ($null -ne $process -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    } catch { $cleanupFailures.Add('Owned process cleanup: ' + $_.Exception.Message) }
    if ($wprStarted) {
        & wpr -stop $scheduler -instancename $sessionName > (Join-Path $out 'wpr-stop.txt')
        if ($LASTEXITCODE -ne 0) {
            # Cancel only our named recorder when saving failed; never touch another recording.
            & wpr -cancel -instancename $sessionName > (Join-Path $out 'wpr-owned-cleanup.txt')
            if ($LASTEXITCODE -ne 0) { $cleanupFailures.Add('Owned Windows recorder cleanup failed') }
            $exitCode = -2
        }
    }
    if ($null -ne $process) { $process.Dispose() }
    [pscustomobject]@{ ExitCode = $exitCode; Utc = [DateTime]::UtcNow.ToString('o'); Mode = $Mode; CleanupFailures = @($cleanupFailures) } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'completion.json')
    Get-ChildItem -LiteralPath $out -File | ForEach-Object {
        [pscustomobject]@{ File = $_.Name; Bytes = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'hashes.json')
    if ($cleanupFailures.Count -eq 0) {
        Write-Output 'STOPPED: owned tracing processes have exited. Preserve raw evidence locally; analyze before any further measurement.'
    } else { Write-Output 'ATTENTION: owned capture cleanup is incomplete; inspect the retained completion record before releasing the window.' }
}
if ($cleanupFailures.Count) { throw 'Capture cleanup is incomplete' }
if ($exitCode -ne 0) { throw 'Capture or scheduler save failed; evidence is incomplete' }
$report = Get-Content -LiteralPath $frames -Raw | ConvertFrom-Json
if ($report.SchemaVersion -lt 2 -or $report.Samples.Count -ne 800 -or $report.Phases.Count -ne 4) { throw 'Incomplete frame evidence; no retry' }
if (!(Test-Path -LiteralPath $trace) -or (Get-Item -LiteralPath $trace).Length -eq 0) { throw 'Missing managed trace; no retry' }
Write-Output 'Capture complete; event loss, marker coverage and stack resolution still require inspection. This is not a budget pass.'
