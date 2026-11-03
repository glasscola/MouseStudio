param(
    [string]$Version = '1.0.0',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '../artifacts')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be a numeric semantic version.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/MouseStudio/MouseStudio.csproj'
$output = [IO.Path]::GetFullPath($OutputRoot)
$bundleName = "MouseStudio-v$Version-win-x64"
$bundle = Join-Path $output $bundleName
if (Test-Path -LiteralPath $bundle) { throw "Output already exists. Choose a new OutputRoot: $bundle" }
$app = Join-Path $bundle 'app'
$null = New-Item -ItemType Directory -Force -Path $app
dotnet restore $project -r win-x64 -p:SelfContained=true --configfile (Join-Path $repoRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -o $app -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$guide = Join-Path $repoRoot 'docs/Mouse-Studio-User-Guide-ko.pdf'
Copy-Item -LiteralPath $guide -Destination $bundle
Copy-Item -LiteralPath $guide -Destination $output
$report = Join-Path $output 'verification.json'
$exe = Join-Path $app 'MouseStudio.exe'
$process = Start-Process -FilePath $exe -ArgumentList @('--verify',('"' + $report + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Verification timed out.' }
if (-not (Test-Path -LiteralPath $report)) { throw 'Verification produced no report.' }
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if (-not $result.passed) { throw 'Verification failed. See verification.json.' }
$archive = Join-Path $output "$bundleName.zip"
Compress-Archive -LiteralPath $bundle -DestinationPath $archive -Force
$files = @($archive,(Join-Path $output 'Mouse-Studio-User-Guide-ko.pdf'))
$checksums = foreach ($file in $files) { '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($file) }
$checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Verified release: $archive"
