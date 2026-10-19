param([string]$Number)
$ErrorActionPreference='Stop'
$probeName='entity'+$Number+'-final-union-probe-'+[Guid]::NewGuid().ToString('N')
$probeRoot=Join-Path $PWD.Path ('artifacts/'+$probeName)
New-Item -ItemType Directory -Path $probeRoot | Out-Null
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start()
$probePort=$listener.LocalEndpoint.Port
$listener.Stop()
$config=(Get-Content config/server.yaml -Raw) -replace '(?m)^name:.*$',('name: '+$probeName) -replace '(?m)^port:.*$',('port: '+$probePort) -replace '(?m)^bind:.*$','bind: 127.0.0.1' -replace '(?m)^data_dir:.*$',('data_dir: artifacts/'+$probeName+'/data')
$configPath=Join-Path $probeRoot 'server.yaml'
[IO.File]::WriteAllText($configPath,$config)
$lease=Join-Path $probeRoot 'server.lease'
[IO.File]::WriteAllText($lease,'alive')
$server=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('src/HollowWake.Server.Host/bin/Debug/net10.0/hollowwake-server.dll','--root',$PWD.Path,'--config',$configPath,'--no-websocket','--lifetime-file',$lease) -WorkingDirectory $PWD.Path -WindowStyle Hidden -RedirectStandardOutput (Join-Path $probeRoot 'server.txt') -RedirectStandardError (Join-Path $probeRoot 'server-errors.txt') -PassThru
$probeOutput='D:/Games/MMOARPG/artifacts/entity'+$Number+'-final-union-probe.txt'
try {
  $ready=$false
  for($i=0;$i -lt 100;$i++) {
    if($server.HasExited){throw 'Private probe server exited before readiness'}
    $client=[Net.Sockets.TcpClient]::new()
    try {$client.Connect('127.0.0.1',$probePort); $ready=$true} catch {} finally {$client.Dispose()}
    if($ready){break}
    Start-Sleep -Milliseconds 100
  }
  if(!$ready){throw 'Private probe server readiness timed out'}
  $features='entity_delta,interface,entry_flow,cast_cues,hit_bodies_v2,projectile_slabs_v1'
  & dotnet src/HollowWake.Tools/bin/Debug/net10.0/hollowwake.dll probe --host 127.0.0.1 --port $probePort --account ($probeName.Substring(0,28)) --seconds 6 --features $features *> $probeOutput
  if($LASTEXITCODE -ne 0){throw 'Private TCP probe failed'}
  $grant=[regex]::Match((Get-Content $probeOutput -Raw),'(?m); granted: ([^\r\n]+)').Groups[1].Value
  foreach ($feature in $features.Split(',')) { if ($grant -notmatch [regex]::Escape($feature)) { throw ('Missing granted feature: '+$feature) } }
  Get-Content $probeOutput -Tail 6
} finally {
  Remove-Item -LiteralPath $lease
  if(!$server.WaitForExit(10000)){$server.Kill($true);throw 'Private probe server failed graceful shutdown'}
}
