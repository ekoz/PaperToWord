# build.ps1 — PaperToWord 可复现构建脚本
# 依赖：仅 Windows 自带的 .NET Framework csc.exe（无需安装任何东西）
# 用法：powershell -ExecutionPolicy Bypass -File build.ps1
# 产物：dist\PaperToWord.exe（含图标 + AppIcon 清单资源）

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'Source'
$dist = Join-Path $root 'dist'
$exe  = Join-Path $dist 'PaperToWord.exe'
$ico  = Join-Path $src  'app.ico'

# 1) 定位系统自带编译器（64 位优先，缺失回退 32 位）
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw '未找到 .NET Framework 4.x 的 csc.exe（Win7 需先装 .NET 4.8）' }

# 2) 源文件清单（新增 .cs 必须同步加到这里）
$files = @('Program.cs','Core.cs','Ooxml.cs','Zip.cs','MainForm.cs','NatSort.cs') |
    ForEach-Object { Join-Path $src $_ }
foreach ($f in $files) { if (-not (Test-Path $f)) { throw "缺少源文件: $f" } }
if (-not (Test-Path $ico)) { throw "缺少图标: $ico" }

# 3) 占用预检：exe 正在运行时给出清晰报错（避免 CS0016 乱码天书）
if (Test-Path $exe) {
    $st = $null
    try { $st = [IO.File]::Open($exe, 'Open', 'ReadWrite', 'None'); $st.Close() }
    catch { throw 'PaperToWord.exe 正被占用：请先关闭正在运行的 PaperToWord 窗口，再重新构建。' }
}

# 4) 编译。要点（勿删）：
#    - C# 5：系统 csc 是 .NET 4.x 自带老编译器，源码禁用 var 之外的新语法（字符串插值/nameof 等）
#    - /win32icon 只设 PE 图标；/resource:app.ico,AppIcon 才让 GetManifestResourceStream("AppIcon") 读到
#    - 引用仅 BCL 四件套，零第三方
& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    "/out:$exe" "/win32icon:$ico" "/resource:$ico,AppIcon" `
    /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @files
if ($LASTEXITCODE -ne 0) { throw "编译失败 exit=$LASTEXITCODE" }

"OK  $exe  $((Get-Item $exe).Length) bytes  $((Get-Item $exe).LastWriteTime)"
