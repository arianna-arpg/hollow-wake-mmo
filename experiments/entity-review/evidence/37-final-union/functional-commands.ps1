param([string]$Number,[string]$Phase='post54')
$ErrorActionPreference='Stop'
$gateRoot='D:/Games/MMOARPG/artifacts'
$gatePrefix=Join-Path $gateRoot ('entity'+$Number+'-'+$Phase+'-')
$source=git rev-parse HEAD
[IO.File]::WriteAllText(($gatePrefix+'source.txt'),$source+"`n",[Text.UTF8Encoding]::new($false))
$gates=@(
  @{Name='build'; Args=@('build','HollowWake.sln','--no-restore','-m:1','-nr:false','-p:UseSharedCompilation=false')},
  @{Name='tests'; Args=@('test','HollowWake.sln','--no-build','-m:1','-nr:false')},
  @{Name='validate'; Args=@('src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll','validate')},
  @{Name='load'; Args=@('src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll','load')},
  @{Name='fight'; Args=@('src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll','fight','skeleton_warrior','zombie')},
  @{Name='census'; Args=@('src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll','weave','--axes','--all','--json',($gatePrefix+'axes.json'),'--literals',($gatePrefix+'literals.txt'))},
  @{Name='release-build'; Args=@('build','src/HollowWake.Tools/HollowWake.Tools.csproj','-c','Release','--no-restore','-m:1','-nr:false','-p:UseSharedCompilation=false')},
  @{Name='release-replay'; Args=@('src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll','tournament','--standard','--check-hashes','experiments/combat-grammar/baselines/standard-duels.json')}
)
foreach ($gate in $gates) {
  $gateArgs=$gate.Args
  & dotnet @gateArgs *> ($gatePrefix+$gate.Name+'.txt')
  $gateExit=$LASTEXITCODE
  Write-Output ($gate.Name+': exit '+$gateExit)
  Get-Content ($gatePrefix+$gate.Name+'.txt') -Tail 4
  if ($gateExit -ne 0) { exit $gateExit }
}
dotnet src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll tournament --seeds 10 --seconds 60 --bands --out ($gatePrefix+'proving.json') *> ($gatePrefix+'proving.txt')
$provingExit=$LASTEXITCODE
[IO.File]::WriteAllText(($gatePrefix+'proving-exit.txt'),$provingExit.ToString()+"`n",[Text.UTF8Encoding]::new($false))
Write-Output ('Proving band gate exit '+$provingExit+'; full report must be compared and failures retained.')
Get-Content ($gatePrefix+'proving.txt') -Tail 5
if ($provingExit -notin @(0,1)) { exit $provingExit }
exit 0
