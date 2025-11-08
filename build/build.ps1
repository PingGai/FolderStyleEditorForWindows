# /build/build.ps1  —— 任何目录执行都可用
# PowerShell 7+ 建议使用：pwsh -File ./build/build.ps1
# Windows PowerShell 5.1 也可正常运行

param(
  [ValidateSet("all","win-x64","win-x86")]
  [string]$Runtime = "all",

  [ValidateSet("Release","Debug")]
  [string]$Configuration = "Release",

  # 基础名（想改就改），用于重命名 exe
  [string]$BaseName = "FolderStyleEditorForWindows"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 仓库根目录：解析脚本的真实路径，确保无论从哪里执行都能找到根目录
$ScriptRealPath = (Get-Item -LiteralPath $PSCommandPath).FullName
$ScriptDir = Split-Path -Parent $ScriptRealPath
$RepoRoot  = (Get-Item -Path $ScriptDir).Parent.FullName

# 项目路径与输出根目录（基于脚本所在位置拼出来）
$ProjRel   = "FolderStyleEditorForWindows_Avalonia\FolderStyleEditorForWindows.csproj"
$ProjPath  = Join-Path $RepoRoot $ProjRel
$PubRoot   = Join-Path $RepoRoot "publish"

if (-not (Test-Path $ProjPath)) {
  Write-Host "❌ 找不到项目文件：$ProjPath" -ForegroundColor Red
  Write-Host "请确认仓库结构为：$RepoRoot\$ProjRel" -ForegroundColor Yellow
  exit 1
}

# 从 build/version.txt 读取版本号
$VersionFile = Join-Path $RepoRoot "build\version.txt"
if (-not (Test-Path $VersionFile)) {
  Write-Host "❌ 找不到版本文件：$VersionFile" -ForegroundColor Red
  exit 1
}
$Version = (Get-Content -LiteralPath $VersionFile -Raw).Trim()
if (-not $Version) {
  Write-Host "❌ 版本文件为空：$VersionFile" -ForegroundColor Red
  exit 1
}

# 需要构建的 RID 列表
$rids = if ($Runtime -eq "all") { @("win-x64","win-x86") } else { @($Runtime) }

foreach ($rid in $rids) {
  $OutDir = Join-Path $PubRoot $rid
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

  Write-Host "==> dotnet clean ($rid, $Configuration)" -ForegroundColor Cyan
  dotnet clean $ProjPath -c $Configuration | Out-Host

  Write-Host "==> dotnet restore" -ForegroundColor Cyan
  dotnet restore $ProjPath | Out-Host

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
    -o $OutDir | Out-Host

  # === 发布后自动重命名主可执行文件 ===
  # 规则：选择输出目录里体积最大的 .exe 作为主程序（单文件发布通常只有一个）
  $exe = Get-ChildItem $OutDir -File -Filter *.exe | Sort-Object Length -Descending | Select-Object -First 1
  if ($exe) {
    $targetName = "$BaseName-$Version-$rid.exe"
    $targetPath = Join-Path $OutDir $targetName
    if (Test-Path $targetPath) { Remove-Item $targetPath -Force }
    Move-Item $exe.FullName $targetPath
    Write-Host "✅ 输出：$targetPath" -ForegroundColor Green
  } else {
    Write-Host "⚠️ 未找到可执行文件（*.exe）于 $OutDir，跳过重命名" -ForegroundColor Yellow
  }
}