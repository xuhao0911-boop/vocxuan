$ErrorActionPreference='Stop'
$taskFramework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$taskOutput=Join-Path $PSScriptRoot 'vocxuan-build'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$vocFontPath=Join-Path $PSScriptRoot 'fonts\NotoSans-Regular.ttf'
$taskIcon=Join-Path $PSScriptRoot 'vocxuan.ico'
if (-not (Test-Path -LiteralPath $taskIcon)) { $taskIcon=Join-Path $PSScriptRoot 'reader.ico' }
$taskArguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',
    ('/out:'+(Join-Path $taskOutput 'VocXuan.exe')),
    ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')),
    ('/win32icon:'+$taskIcon),
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Xaml.dll','/reference:System.Web.Extensions.dll',
    ('/reference:'+(Join-Path $taskFramework 'WPF\PresentationFramework.dll')),
    ('/reference:'+(Join-Path $taskFramework 'WPF\PresentationCore.dll')),
    ('/reference:'+(Join-Path $taskFramework 'WPF\WindowsBase.dll')),
    ('/reference:'+(Join-Path $taskFramework 'WPF\System.Speech.dll')),
    ('/resource:'+(Join-Path $PSScriptRoot 'dictionary.tsv.gz')+',dictionary.gz'),
    ('/resource:'+$vocFontPath+',VocXuan.IPA.ttf'))
$taskArguments+=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'VocXuan.*.cs' | ForEach-Object { $_.FullName })
& (Join-Path $taskFramework 'csc.exe') @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Get-Item -LiteralPath (Join-Path $taskOutput 'VocXuan.exe') | Select-Object FullName,Length
