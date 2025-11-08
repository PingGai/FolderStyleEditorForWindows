# 使用 powershell.exe 执行 build.ps1 脚本
# 这避免了对 pwsh 环境变量的依赖

# 获取当前脚本所在的目录
$ScriptDir = Split-Path -Parent $PSCommandPath

# 构建 build.ps1 的完整路径
$BuildScriptPath = Join-Path $ScriptDir "build.ps1"

# 执行 build.ps1，并将所有参数传递给它
powershell.exe -ExecutionPolicy Bypass -File $BuildScriptPath @args