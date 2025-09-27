# /build/build.ps1  —— 任何目录执行都可用
# PowerShell 7+ 建议使用：pwsh -File ./build/build.ps1

param(
  [ValidateSet("all","win-x64","win-x86")]
  [string]$Runtime = "all",

  [ValidateSet("Release","Debug")]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# 统一控制台编码为 UTF-8（避免中文提示乱码）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 仓库根目录：脚本在 /build 下，所以取其父目录
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot  = Split-Path -Parent $ScriptDir

# 项目路径与输出根目录（基于脚本所在位置拼出来）
$ProjRel   = "WindowsFolderStyleEditor_Avalonia\WindowsFolderStyleEditor_Avalonia.csproj"
$ProjPath  = Join-Path $RepoRoot $ProjRel
$PubRoot   = Join-Path $RepoRoot "publish"

if (-not (Test-Path $ProjPath)) {
  Write-Host "❌ 找不到项目文件：$ProjPath" -ForegroundColor Red
  Write-Host "请确认仓库结构为：$RepoRoot\$ProjRel" -ForegroundColor Yellow
  exit 1
}

# 需要构建的 RID 列表
$rids = if ($Runtime -eq "all") { @("win-x64","win-x86") } else { @($Runtime) }

foreach ($rid in $rids) {
  $OutDir = Join-Path $PubRoot $rid
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

  Write-Host "==> dotnet clean ($rid, $Configuration)" -ForegroundColor Cyan
  dotnet clean $ProjPath -c $Configuration

  Write-Host "==> dotnet restore" -ForegroundColor Cyan
  dotnet restore $ProjPath

  Write-Host "==> dotnet publish ($rid, $Configuration)" -ForegroundColor Cyan
  dotnet publish $ProjPath `
    -c $Configuration `
    -r $rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -o $OutDir

  Write-Host "✅ 输出目录：$OutDir" -ForegroundColor Green
}
