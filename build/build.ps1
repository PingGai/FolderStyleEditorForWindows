# /build/build.ps1  —— 任何目录执行都可用
# PowerShell 7+：pwsh -File ./build/build.ps1
# Windows PowerShell 5.1：powershell -File .\build\build.ps1

param(
    [ValidateSet("all","win-x64","win-x86")]
    [string]$Runtime = "all",

    [ValidateSet("Release","Debug")]
    [string]$Configuration = "Release",

    # 基础名（dotnet publish 生成的原始 exe 名称）
    [string]$BaseName = "FolderStyleEditorForWindows"
)

$ErrorActionPreference = "Stop"

# 控制台编码，兼容旧版 PowerShell，尽量避免乱码
try {
    if ($PSVersionTable.PSVersion.Major -lt 6 -and $env:OS -like "*Windows*") {
        chcp 65001 > $null 2>&1
    }
} catch {}

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

# 仓库根目录：解析脚本真实路径，确保无论从哪里执行都能找到根目录
$ScriptRealPath = (Get-Item -LiteralPath $PSCommandPath).FullName
$ScriptDir      = Split-Path -Parent $ScriptRealPath
$RepoRoot       = (Get-Item -Path $ScriptDir).Parent.FullName

# 项目路径与输出根目录
$ProjRel   = "FolderStyleEditorForWindows_Avalonia\FolderStyleEditorForWindows.csproj"
$ProjPath  = Join-Path $RepoRoot $ProjRel
$PubRoot   = Join-Path $RepoRoot "publish"

if (-not (Test-Path -LiteralPath $ProjPath)) {
    Write-Host "找不到项目文件：$ProjPath" -ForegroundColor Red
    Write-Host "请确认仓库结构为：$RepoRoot\$ProjRel" -ForegroundColor Yellow
    exit 1
}

# 从 build/version.txt 读取版本号
$VersionFile = Join-Path $RepoRoot "build\version.txt"
if (-not (Test-Path -LiteralPath $VersionFile)) {
    Write-Host "找不到版本文件：$VersionFile" -ForegroundColor Red
    exit 1
}

$Version = (Get-Content -LiteralPath $VersionFile -Raw).Trim()
if (-not $Version) {
    Write-Host "版本文件为空：$VersionFile" -ForegroundColor Red
    exit 1
}

# 需要构建的 RID 列表
if ($Runtime -eq "all") {
    $rids = @("win-x64","win-x86")
} else {
    $rids = @($Runtime)
}

foreach ($rid in $rids) {

    $OutDir = Join-Path $PubRoot $rid
    if (-not (Test-Path -LiteralPath $OutDir)) {
        New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    }

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
    # 优先：明确使用 $BaseName.exe 作为源文件名，避免把“目标名”误当源
    $srcExeName  = "$BaseName.exe"
    $srcExePath  = Join-Path $OutDir $srcExeName
    $targetName  = "$BaseName-$Version-$rid.exe"
    $targetPath  = Join-Path $OutDir $targetName

    if (Test-Path -LiteralPath $srcExePath) {
        if (Test-Path -LiteralPath $targetPath) {
            Remove-Item -LiteralPath $targetPath -Force
        }

        # Rename-Item 在同一目录内改名，更直观
        Rename-Item -LiteralPath $srcExePath -NewName $targetName
        Write-Host "输出：$targetPath" -ForegroundColor Green
    }
    else {
        # 兜底：如果找不到 $BaseName.exe，就从目录里找体积最大 exe 作为主程序
        $exe = Get-ChildItem -LiteralPath $OutDir -File -Filter *.exe |
               Sort-Object Length -Descending |
               Select-Object -First 1

        if ($exe) {
            if (Test-Path -LiteralPath $targetPath) {
                Remove-Item -LiteralPath $targetPath -Force
            }

            Rename-Item -LiteralPath $exe.FullName -NewName $targetName
            Write-Host "输出（使用候选 exe）：$targetPath" -ForegroundColor Green
        }
        else {
            Write-Host "未找到 $BaseName.exe 或任何 *.exe 于 $OutDir，跳过重命名" -ForegroundColor Yellow
        }
    }
}
