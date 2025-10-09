<# 
  Clean-Leadtools-WpfMaxViewer.ps1
  目标：仅保留 WPF 本地渲染/查看所需模块；剔除非渲染类组件（条码、CCOW、信用卡、数据访问层、媒体写入、
       屏幕抓取、MRC 压缩、注释设计器/自动化/医疗包、WinForms 变体、服务端等），并清理 .xml 文档文件。
  用法：
    Set-ExecutionPolicy Bypass -Scope Process -Force
    .\Clean-Leadtools-WpfMaxViewer.ps1 -Path .            # 移动到 _Removed（推荐先试）
    .\Clean-Leadtools-WpfMaxViewer.ps1 -Path . -Delete     # 直接删除（危险）
#>

param(
  [Parameter(Mandatory=$false)]
  [string]$Path = ".",
  [switch]$Delete
)

$root = Resolve-Path $Path
Set-Location $root

# 处理这些扩展
$targets = @("*.dll","*.xml","*.json","*.exe","*.pdb","*.config","*.deps.json","*.runtimeconfig.json")

Write-Host "工作目录: $root"
Write-Host ("模式: {0}" -f ($(if ($Delete) { "直接删除（危险）" } else { "移至 _Removed（推荐先试跑）" }))) `
  -ForegroundColor ($(if ($Delete) { "Yellow" } else { "Green" }))

# ========= 保留（白名单）=========
# 覆盖：核心、WPF 控件、全部 Codecs、图像处理、文档/PDF、矢量/SVG、WPF 渲染基类、注释（仅渲染必需）、JPEG2000
$keepPatterns = @(
  "^Leadtools\.dll$",
  "^Leadtools\.Drawing(\.xml)?$",
  "^Leadtools\.Controls\.Wpf\.dll$",
  "^Leadtools\.Windows\.Media(\..+)?\.dll$",
  "^Leadtools\.Windows\.Controls\.dll$",
  "^Leadtools\.Windows\.D2DRendering\.dll$",

  "^Leadtools\.Codecs(\.xml)?$",
  "^Leadtools\.Codecs\..+\.dll$",

  "^Leadtools\.ImageProcessing\..+\.dll$",
  "^Leadtools\.ImageOptimization\.dll$",

  "^Leadtools\.Documents(\..+)?\.dll$",
  "^Leadtools\.Pdf(\.xml)?$",
  "^Leadtools\.Pdf(Engine|Compressor)?\.dll$",
  "^Leadtools\.Svg\.dll$",
  "^Leadtools\.Vector\.dll$",

  "^Leadtools\.Annotations(\.Core|\.dll)$",
  "^Leadtools\.Annotations\.Wpf\.dll$",
  "^Leadtools\.Annotations\.Rendering\.Wpf\.dll$",
  "^Leadtools\.Kernel\.Annotations\.dll$",

  "^Leadtools\.Windows\.Annotations\.dll$",

  "^Leadtools\.Jpeg2000\.dll$"
)

# ========= 强制移除（黑名单，优先级更高）=========
$removePatterns = @(
  # 非 WPF 平台/变体
  "^Leadtools\..*WinForms.*\.dll$",
  "^Leadtools\.Annotations\.Rendering\.WinForms\.dll$",
  "^Leadtools\.Documents\.UI\.WinForms\.dll$",

  # 注释：设计器/自动化/医疗包/旧版
  "^Leadtools\.Annotations\.Designers\.dll$",
  "^Leadtools\.Annotations\.Automation\.dll$",
  "^Leadtools\.Annotations\.UserMedicalPack\..+\.dll$",
  "^Leadtools\.Annotations\.Legacy\.dll$",
  "^Leadtools\.Annotations\.Documents\.dll$",

  # 条码（读写/2D/QR/PDF等；非纯渲染）
  "^Leadtools\.Barcode(\..+)?\.dll$",

  # CCOW/临床上下文
  "^Leadtools\.Ccow.*\.dll$",
  "^Leadtools\.CcowWebParticipant\.Plugin\.dll$",

  # 信用卡识别/业务
  "^Leadtools\.CreditCards\.dll$",

  # 数据访问层/服务器侧
  "^Leadtools\.DataAccessLayers(\..+)?\.dll$",

  # 媒体写入、屏幕抓取、MRC 压缩（非渲染）
  "^Leadtools\.MediaWriter\.dll$",
  "^Leadtools\.ScreenCapture\.dll$",
  "^Leadtools\.Mrc\.dll$",

  # 服务/WCF/作业/服务器端
  "^Leadtools\.Services(\..+)?\.dll$",
  "^Leadtools\.JobProcessor(\..+)?\.dll$",
  "^Leadtools\.Web\.dll$",
  "^Leadtools\.Wcf\..+\.dll$",

  # 其它企业/周边
  "^Leadtools\.SharePoint\.Client\.dll$",
  "^Leadtools\.Smartcard\.dll$",

  # 示例/预览/插件/Agent/Server/DataAccess 等
  "^LEADTOOLSPreview\.exe$",
  "^LEADTOOLSPreview\..+$",
  "^Leadtools\..*\.Agent\.dll$",
  "^Leadtools\..*\.AddIn\.dll$",
  "^Leadtools\..*Server(\.|$).+\.dll$",
  "^Leadtools\..*WebViewer(\..+)?\.dll$",
  "^Leadtools\..*Workstation(\..+)?\.dll$",
  "^Leadtools\..*Gateway(\..+)?\.dll$",

  # 统一清理所有 .xml 文档（不是运行时必需）
  ".*\.xml$"
)

# ========= 可能的杂项（未命中白/黑名单时再考虑移除）=========
$maybeJunkPatterns = @(
  "^Leadtools\.SpecialEffects\.dll$",    # 可选：纯特效，非必要渲染
  "^Leadtools\.ColorConversion\.dll$",   # 可选：颜色转换（通常可不需要）
  "^Leadtools\.Caching\.dll$"            # 可选：性能缓存（非必须）
)

# ===== 文件扫描 =====
$allFiles = foreach ($p in $targets) { Get-ChildItem -Path $root -Filter $p -Recurse -File -ErrorAction SilentlyContinue }

# _Removed 目录
$removedDir = Join-Path $root "_Removed"
if (-not $Delete) {
  if (-not (Test-Path $removedDir)) { New-Item -ItemType Directory -Path $removedDir | Out-Null }
}

function Should-MatchAny($name, [string[]]$patterns) {
  foreach ($pat in $patterns) { if ($name -match $pat) { return $true } }
  return $false
}

[int]$moved = 0; [int]$deleted = 0; [int]$kept = 0

foreach ($f in $allFiles) {
  $n = $f.Name

  if (Should-MatchAny $n $removePatterns) {
    if ($Delete) {
      Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue
      Write-Host "删除: $n"
      $deleted++
    } else {
      $dest = Join-Path $removedDir $n
      Move-Item -LiteralPath $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
      Write-Host "移走: $n"
      $moved++
    }
    continue
  }

  if (Should-MatchAny $n $keepPatterns) {
    Write-Host "保留: $n"
    $kept++
    continue
  }

  # 未命中白/黑名单 -> 看是否属于“杂项”
  if (Should-MatchAny $n $maybeJunkPatterns) {
    if ($Delete) {
      Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue
      Write-Host "删除(杂项): $n"
      $deleted++
    } else {
      $dest = Join-Path $removedDir $n
      Move-Item -LiteralPath $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
      Write-Host "移走(杂项): $n"
      $moved++
    }
  } else {
    # 保守：默认保留，避免误删潜在渲染依赖
    Write-Host "保留(默认): $n"
    $kept++
  }
}

Write-Host ""
if ($Delete) {
  Write-Host "完成：保留 $kept 个文件，删除 $deleted 个文件。"
} else {
  Write-Host "完成：保留 $kept 个文件，移走 $moved 个文件 到 $removedDir。"
}
