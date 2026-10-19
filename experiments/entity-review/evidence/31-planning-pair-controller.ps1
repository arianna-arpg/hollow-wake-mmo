$ErrorActionPreference='Stop'
$pairRoot='<pair-root>'
$pairDotnetVersion=$pairDotnetVersion
foreach ($name in @('mainfa7','entity31')) {
 $root=Join-Path $pairRoot $name
 $manifest=Get-Content -LiteralPath (Join-Path $root 'source-manifest.json') -Raw | ConvertFrom-Json
 foreach ($file in $manifest.files.psobject.Properties) {
  if ((Get-FileHash -LiteralPath (Join-Path $root $file.Name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.Value) { throw "Archive changed: $name $($file.Name)" }
 }
 foreach ($suffix in @('worldgen.json','stderr.txt','metadata.json')) { if (Test-Path -LiteralPath (Join-Path $pairRoot ($name+'-'+$suffix))) { throw "First sample already exists: $name $suffix" } }
}
foreach ($name in @('mainfa7','entity31')) {
 $root=Join-Path $pairRoot $name
 $manifest=Get-Content -LiteralPath (Join-Path $root 'source-manifest.json') -Raw | ConvertFrom-Json
 $tool=Join-Path $root 'src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll'
 $output=Join-Path $pairRoot ($name+'-worldgen.json')
 $errorFile=Join-Path $pairRoot ($name+'-stderr.txt')
 $started=[DateTime]::UtcNow.ToString('o')
 $binaryHashes=@(Get-ChildItem -LiteralPath (Split-Path $tool) -Filter '*.dll' | Get-FileHash -Algorithm SHA256 | Select-Object @{Name='File';Expression={Split-Path $_.Path -Leaf}},Hash)
 $job=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($tool,'bench','--only','worldgen','--json') -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $output -RedirectStandardError $errorFile -PassThru
 $job.PriorityClass='Normal'
 $metadata=[ordered]@{Source=$manifest.source;ContentTree=$manifest.contentTree;ArchiveSha256=$manifest.archiveSha256;SourceFilesVerified=@($manifest.files.psobject.Properties).Count;ProcessId=$job.Id;Priority=$job.PriorityClass.ToString();Command='dotnet src/HollowWake.Tools/bin/Release/net10.0/hollowwake.dll bench --only worldgen --json';OutputMode='JSON redirected from standard output';StartedUtc=$started;Binaries=$binaryHashes;NoRetry=$true;Seed='aureth';Order=$name;DotnetVersion=$pairDotnetVersion}
 $job.WaitForExit()
 $metadata.CompletedUtc=[DateTime]::UtcNow.ToString('o');$metadata.ExitCode=$job.ExitCode
 $metadata | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $pairRoot ($name+'-metadata.json')) -Encoding utf8
 Write-Output "$name exit $($job.ExitCode)"
 Get-Content -LiteralPath $output
 if ($job.ExitCode -ne 0) { throw "First benchmark failed; retained $name" }
}
