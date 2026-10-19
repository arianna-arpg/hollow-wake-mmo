param([string]$Number)
$ErrorActionPreference='Stop'
$artifactRoot='D:/Games/MMOARPG/artifacts'
git rev-parse HEAD > (Join-Path $artifactRoot ('entity'+$Number+'-final-union-client-source.txt'))
if (!(Test-Path web/node_modules)) {
  npm.cmd --prefix web ci *> (Join-Path $artifactRoot ('entity'+$Number+'-final-union-web-install.txt'))
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$gates=@(
  @{Name='web-tests'; File='npm.cmd'; Args=@('--prefix','web','test')},
  @{Name='web-build'; File='npm.cmd'; Args=@('--prefix','web','run','build')},
  @{Name='porter'; File='node'; Args=@('--test','scripts/port/port-prototype.test.mjs')},
  @{Name='protocol'; File='dotnet'; Args=@('src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll','protocol-ts','--check')},
  @{Name='smoke'; File='node'; Args=@('scripts/test-background.mjs','--suite','all','--no-build')}
)
foreach ($gate in $gates) {
  $gateFile=$gate.File; $gateArgs=$gate.Args
  & $gateFile @gateArgs *> (Join-Path $artifactRoot ('entity'+$Number+'-final-union-'+$gate.Name+'.txt'))
  $gateExit=$LASTEXITCODE
  Write-Output ($gate.Name+': exit '+$gateExit)
  Get-Content (Join-Path $artifactRoot ('entity'+$Number+'-final-union-'+$gate.Name+'.txt')) -Tail 10
  if ($gateExit -ne 0) { exit $gateExit }
}
& D:/Games/MMOARPG/artifacts/entity-final-union-probe.ps1 $Number
