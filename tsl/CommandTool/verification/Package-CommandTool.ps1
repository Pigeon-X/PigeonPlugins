$ErrorActionPreference='Stop'
$project='E:\流光核心源码\_git\PigeonPlugins\tsl\CommandTool'
$workspace=(Get-Item (Join-Path $project '..\..\..\..')).FullName
$release=Join-Path $workspace '_发布\CommandTool_UTSL_2.0.0_20261019'
if(Test-Path -LiteralPath $release){throw "发布目录已存在，拒绝覆盖：$release"}
$dll=Join-Path $project 'out\CommandTool.dll'
$loaded=Join-Path $project 'verification\loader\artifacts\release\plugins\CommandTool\CommandTool.dll'
if(!(Test-Path -LiteralPath $loaded)){throw '缺少最终加载器实际加载的DLL'}
if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $loaded -Algorithm SHA256).Hash){throw '实际加载DLL与交付DLL不一致'}
$test=Join-Path $project 'tests\test-result-release.log'
$loader=Join-Path $project 'verification\loader\artifacts\release\loader-release.log'
if(![IO.File]::ReadAllText($test).Contains('TOTAL 96 / PASS 96 / FAIL 0')){throw '回归测试结果不符合发布门槛'}
if(![IO.File]::ReadAllText($loader).Contains('RESULT PASS=14 FAIL=0')){throw '最终加载验证未通过'}
$install=Join-Path $release '安装内容'
$source=Join-Path $release '源码内容\CommandTool'
New-Item -ItemType Directory -Path (Join-Path $install 'plugins'),(Join-Path $install 'examples'),(Join-Path $source 'native'),(Join-Path $source 'tests'),(Join-Path $source 'verification\loader'),(Join-Path $source 'verification\evidence'),(Join-Path $source 'examples') -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $install 'plugins\CommandTool.dll')
foreach($name in @('README_UTSL_2.0.md','验证说明_2.0.md')){
  Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $install $name)
  Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $source $name)
}
Copy-Item -LiteralPath (Join-Path $project 'examples\CommandTool.v2.json') -Destination (Join-Path $install 'examples\CommandTool.v2.json')
Copy-Item -LiteralPath (Join-Path $project 'examples\CommandTool.v2.json') -Destination (Join-Path $source 'examples\CommandTool.v2.json')
Copy-Item -LiteralPath (Join-Path $project 'CommandTool.UnifierTSL.csproj') -Destination $source
Get-ChildItem -LiteralPath (Join-Path $project 'native') -File -Filter '*.cs' | Copy-Item -Destination (Join-Path $source 'native')
Get-ChildItem -LiteralPath (Join-Path $project 'tests') -File | Where-Object {$_.Extension -in '.cs','.csproj'} | Copy-Item -Destination (Join-Path $source 'tests')
foreach($name in @('Program.cs','LoaderProbe.csproj')){Copy-Item -LiteralPath (Join-Path $project ('verification\loader\'+$name)) -Destination (Join-Path $source 'verification\loader')}
Copy-Item -LiteralPath $test -Destination (Join-Path $source 'verification\evidence\tests-release.log')
Copy-Item -LiteralPath $loader -Destination (Join-Path $source 'verification\evidence\loader-release.log')
Copy-Item -LiteralPath (Join-Path $project 'verification\build-release.log') -Destination (Join-Path $source 'verification\evidence\build-release.log')
$utf8=[Text.UTF8Encoding]::new($false)
function Write-Hashes([string]$root){
  $lines=Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object{
    $relative=$_.FullName.Substring($root.Length+1).Replace('\','/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$relative
  }
  [IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'),[string[]]$lines,$utf8)
}
Write-Hashes $install
Write-Hashes $source
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$installZip=Join-Path $release 'CommandTool_UTSL_2.0.0_安装包.zip'
$sourceZip=Join-Path $release 'CommandTool_UTSL_2.0.0_源码与验证.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($install,$installZip,[IO.Compression.CompressionLevel]::Optimal,$false)
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $source -Parent),$sourceZip,[IO.Compression.CompressionLevel]::Optimal,$false)
$zip=[IO.Compression.ZipFile]::OpenRead($installZip)
try{
  $entry=$zip.Entries | Where-Object {$_.FullName.Replace('\','/') -eq 'plugins/CommandTool.dll'}
  if(!$entry){throw '安装压缩包缺少唯一插件DLL'}
  if(@($zip.Entries | Where-Object {$_.FullName.EndsWith('.dll',[StringComparison]::OrdinalIgnoreCase)}).Count -ne 1){throw '安装压缩包混入核心依赖'}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$zippedHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
  if($zippedHash -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash){throw '安装压缩包中的插件哈希不一致'}
}finally{$zip.Dispose()}
$manifest=[ordered]@{
  Plugin='CommandTool';Version='2.0.0';Platform='UnifierTSL 0.3.1 / TShock 6.1 / Terraria 1.4.5.8';Target='net9.0'
  DllBytes=(Get-Item -LiteralPath $dll).Length;DllSha256=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
  Tests='96/96 (migration is one suite with internal assertions)';NativeLoader='14/14';ClientNetworkTested=$false
  InstallZip=Split-Path $installZip -Leaf;SourceZip=Split-Path $sourceZip -Leaf
  InstallZipSha256=(Get-FileHash -LiteralPath $installZip -Algorithm SHA256).Hash
  SourceZipSha256=(Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText((Join-Path $release '发布清单.json'),($manifest|ConvertTo-Json -Depth 5),$utf8)
Get-Item -LiteralPath $installZip,$sourceZip | Select-Object FullName,Length
$manifest|ConvertTo-Json -Depth 5
