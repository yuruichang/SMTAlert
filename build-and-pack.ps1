$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir "SMTAlert.csproj"

# 读取版本号
[xml]$csproj = Get-Content $ProjectFile
$version = ($csproj.Project.PropertyGroup | Select-Object -First 1).Version
if (-not $version) { $version = "1.0" }
Write-Host "Version: $version"

# 编译 Release|x64
Write-Host "Building Release|x64 ..."
dotnet build $ProjectFile -c Release -p:Platform=x64 --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# 打包 zip
$sourceDir = Join-Path $ProjectDir "bin\x64\Release\net8.0-windows"
$zipName = "SMTAlert-v$version.zip"
$zipPath = Join-Path $ProjectDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourceDir, $zipPath)

Write-Host "Packaged: $zipName" -ForegroundColor Green
$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Size: $size MB"
Read-Host "Press Enter to exit"