$ErrorActionPreference = 'Stop'
$evidence = 'experiments/golems/evidence/corrected-foundation'
$records = [System.Collections.Generic.List[object]]::new()
function Invoke-Gate([string]$name, [string]$exe, [string[]]$arguments, [int]$expected = 0) {
    $started = [DateTime]::UtcNow.ToString('o')
    & $exe @arguments *> "$evidence/$name.txt"
    $code = $LASTEXITCODE
    $records.Add([ordered]@{name=$name; executable=$exe; arguments=$arguments; startedUtc=$started; finishedUtc=[DateTime]::UtcNow.ToString('o'); exitCode=$code; expectedExit=$expected})
    [IO.File]::WriteAllText((Join-Path (Get-Location) "$evidence/commands.json"), ($records | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
    Write-Output "$name exit=$code expected=$expected"
    if ($code -ne $expected) { Get-Content "$evidence/$name.txt" -Tail 30; throw "$name failed" }
}
Invoke-Gate 'release-build' 'dotnet' @('build','src/HollowWake.Tools/HollowWake.Tools.csproj','-c','Release','--no-restore','-m:1','-nr:false','-p:UseSharedCompilation=false')
Invoke-Gate 'web-tests' 'npm.cmd' @('--prefix','web','test')
Invoke-Gate 'web-build' 'npm.cmd' @('--prefix','web','run','build')
Invoke-Gate 'porter' 'node' @('--test','scripts/port/port-prototype.test.mjs')
Invoke-Gate 'lifecycle-tests' 'node' @('--test','scripts/test-background.test.mjs')
$debugTool = 'src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll'
$releaseTool = 'src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll'
Invoke-Gate 'validate' 'dotnet' @($debugTool,'validate')
Invoke-Gate 'protocol' 'dotnet' @($debugTool,'protocol-ts','--check')
Invoke-Gate 'axes' 'dotnet' @($debugTool,'weave','--axes','--all','--json','experiments/golems/artifacts/corrected-axes.json','--literals',"$evidence/frozen-literals.txt")
Invoke-Gate 'load' 'dotnet' @($debugTool,'load','--x','0','--y','0')
Invoke-Gate 'fight' 'dotnet' @($debugTool,'fight','amalgam_horror','stone_golem')
Invoke-Gate 'replay-release' 'dotnet' @($releaseTool,'tournament','--standard','--check-hashes','experiments/combat-grammar/baselines/standard-duels.json','--out',"$evidence/standard-actual.json")
Invoke-Gate 'proving' 'dotnet' @($releaseTool,'tournament','--seeds','10','--seconds','60','--bands','--out',"$evidence/proving.json") 1
Invoke-Gate 'world-audit-build' 'dotnet' @('build','experiments/golems/artifacts/LevelAudit/LevelAudit.csproj','--no-restore','-m:1','-nr:false','-p:UseSharedCompilation=false')
Invoke-Gate 'world-audit' 'dotnet' @('experiments/golems/artifacts/LevelAudit/bin/Debug/net10.0/LevelAudit.dll',"$evidence/world-after.json")
