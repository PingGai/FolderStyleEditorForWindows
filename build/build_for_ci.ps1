param(
    [ValidateSet("all","win-x64","win-x86")]
    [string]$Runtime = "all",

    [ValidateSet("Release","Debug")]
    [string]$Configuration = "Release",

    [string]$BaseName = "FolderStyleEditorForWindows"
)

$ErrorActionPreference = "Stop"

try {
    if ($PSVersionTable.PSVersion.Major -lt 6 -and $env:OS -like "*Windows*") {
        chcp 65001 > $null 2>&1
    }
} catch {}

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

$ScriptRealPath = (Get-Item -LiteralPath $PSCommandPath).FullName
$ScriptDir      = Split-Path -Parent $ScriptRealPath
$RepoRoot       = (Get-Item -Path $ScriptDir).Parent.FullName

$ProjRel   = "FolderStyleEditorForWindows_Avalonia\FolderStyleEditorForWindows.csproj"
$ProjPath  = Join-Path $RepoRoot $ProjRel
$PubRoot   = Join-Path $RepoRoot "publish"

if (-not (Test-Path -LiteralPath $ProjPath)) {
    Write-Host "找不到项目文件：$ProjPath" -ForegroundColor Red
    exit 1
}

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

    # ❌ 不再在循环里 clean / restore
    # Write-Host "==> dotnet clean ($rid, $Configuration)" -ForegroundColor Cyan
    # dotnet clean $ProjPath -c $Configuration | Out-Host

    # Write-Host "==> dotnet restore" -ForegroundColor Cyan
    # dotnet restore $ProjPath | Out-Host

    Write-Host "==> dotnet publish ($rid, $Configuration)" -ForegroundColor Cyan
    dotnet publish $ProjPath `
        -c $Configuration `
        -r $rid `
        --no-restore `                     # ✅ 关键：依赖由外部提前 restore
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -o $OutDir | Out-Host

    $srcExeName  = "$BaseName.exe"
    $srcExePath  = Join-Path $OutDir $srcExeName
    $targetName  = "$BaseName-$Version-$rid.exe"
    $targetPath  = Join-Path $OutDir $targetName

    if (Test-Path -LiteralPath $srcExePath) {
        if (Test-Path -LiteralPath $targetPath) {
            Remove-Item -LiteralPath $targetPath -Force
        }
        Rename-Item -LiteralPath $srcExePath -NewName $targetName
        Write-Host "输出：$targetPath" -ForegroundColor Green
    }
    else {
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
