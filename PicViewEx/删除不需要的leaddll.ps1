<# 
  Clean-Leadtools-WpfMaxViewer.ps1
  Ŀ�꣺������ WPF ������Ⱦ/�鿴����ģ�飻�޳�����Ⱦ����������롢CCOW�����ÿ������ݷ��ʲ㡢ý��д�롢
       ��Ļץȡ��MRC ѹ����ע�������/�Զ���/ҽ�ư���WinForms ���塢����˵ȣ��������� .xml �ĵ��ļ���
  �÷���
    Set-ExecutionPolicy Bypass -Scope Process -Force
    .\Clean-Leadtools-WpfMaxViewer.ps1 -Path .            # �ƶ��� _Removed���Ƽ����ԣ�
    .\Clean-Leadtools-WpfMaxViewer.ps1 -Path . -Delete     # ֱ��ɾ����Σ�գ�
#>

param(
  [Parameter(Mandatory=$false)]
  [string]$Path = ".",
  [switch]$Delete
)

$root = Resolve-Path $Path
Set-Location $root

# ������Щ��չ
$targets = @("*.dll","*.xml","*.json","*.exe","*.pdb","*.config","*.deps.json","*.runtimeconfig.json")

Write-Host "����Ŀ¼: $root"
Write-Host ("ģʽ: {0}" -f ($(if ($Delete) { "ֱ��ɾ����Σ�գ�" } else { "���� _Removed���Ƽ������ܣ�" }))) `
  -ForegroundColor ($(if ($Delete) { "Yellow" } else { "Green" }))

# ========= ��������������=========
# ���ǣ����ġ�WPF �ؼ���ȫ�� Codecs��ͼ�����ĵ�/PDF��ʸ��/SVG��WPF ��Ⱦ���ࡢע�ͣ�����Ⱦ���裩��JPEG2000
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

# ========= ǿ���Ƴ��������������ȼ����ߣ�=========
$removePatterns = @(
  # �� WPF ƽ̨/����
  "^Leadtools\..*WinForms.*\.dll$",
  "^Leadtools\.Annotations\.Rendering\.WinForms\.dll$",
  "^Leadtools\.Documents\.UI\.WinForms\.dll$",

  # ע�ͣ������/�Զ���/ҽ�ư�/�ɰ�
  "^Leadtools\.Annotations\.Designers\.dll$",
  "^Leadtools\.Annotations\.Automation\.dll$",
  "^Leadtools\.Annotations\.UserMedicalPack\..+\.dll$",
  "^Leadtools\.Annotations\.Legacy\.dll$",
  "^Leadtools\.Annotations\.Documents\.dll$",

  # ���루��д/2D/QR/PDF�ȣ��Ǵ���Ⱦ��
  "^Leadtools\.Barcode(\..+)?\.dll$",

  # CCOW/�ٴ�������
  "^Leadtools\.Ccow.*\.dll$",
  "^Leadtools\.CcowWebParticipant\.Plugin\.dll$",

  # ���ÿ�ʶ��/ҵ��
  "^Leadtools\.CreditCards\.dll$",

  # ���ݷ��ʲ�/��������
  "^Leadtools\.DataAccessLayers(\..+)?\.dll$",

  # ý��д�롢��Ļץȡ��MRC ѹ��������Ⱦ��
  "^Leadtools\.MediaWriter\.dll$",
  "^Leadtools\.ScreenCapture\.dll$",
  "^Leadtools\.Mrc\.dll$",

  # ����/WCF/��ҵ/��������
  "^Leadtools\.Services(\..+)?\.dll$",
  "^Leadtools\.JobProcessor(\..+)?\.dll$",
  "^Leadtools\.Web\.dll$",
  "^Leadtools\.Wcf\..+\.dll$",

  # ������ҵ/�ܱ�
  "^Leadtools\.SharePoint\.Client\.dll$",
  "^Leadtools\.Smartcard\.dll$",

  # ʾ��/Ԥ��/���/Agent/Server/DataAccess ��
  "^LEADTOOLSPreview\.exe$",
  "^LEADTOOLSPreview\..+$",
  "^Leadtools\..*\.Agent\.dll$",
  "^Leadtools\..*\.AddIn\.dll$",
  "^Leadtools\..*Server(\.|$).+\.dll$",
  "^Leadtools\..*WebViewer(\..+)?\.dll$",
  "^Leadtools\..*Workstation(\..+)?\.dll$",
  "^Leadtools\..*Gateway(\..+)?\.dll$",

  # ͳһ�������� .xml �ĵ�����������ʱ���裩
  ".*\.xml$"
)

# ========= ���ܵ����δ���а�/������ʱ�ٿ����Ƴ���=========
$maybeJunkPatterns = @(
  "^Leadtools\.SpecialEffects\.dll$",    # ��ѡ������Ч���Ǳ�Ҫ��Ⱦ
  "^Leadtools\.ColorConversion\.dll$",   # ��ѡ����ɫת����ͨ���ɲ���Ҫ��
  "^Leadtools\.Caching\.dll$"            # ��ѡ�����ܻ��棨�Ǳ��룩
)

# ===== �ļ�ɨ�� =====
$allFiles = foreach ($p in $targets) { Get-ChildItem -Path $root -Filter $p -Recurse -File -ErrorAction SilentlyContinue }

# _Removed Ŀ¼
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
      Write-Host "ɾ��: $n"
      $deleted++
    } else {
      $dest = Join-Path $removedDir $n
      Move-Item -LiteralPath $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
      Write-Host "����: $n"
      $moved++
    }
    continue
  }

  if (Should-MatchAny $n $keepPatterns) {
    Write-Host "����: $n"
    $kept++
    continue
  }

  # δ���а�/������ -> ���Ƿ����ڡ����
  if (Should-MatchAny $n $maybeJunkPatterns) {
    if ($Delete) {
      Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue
      Write-Host "ɾ��(����): $n"
      $deleted++
    } else {
      $dest = Join-Path $removedDir $n
      Move-Item -LiteralPath $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
      Write-Host "����(����): $n"
      $moved++
    }
  } else {
    # ���أ�Ĭ�ϱ�����������ɾǱ����Ⱦ����
    Write-Host "����(Ĭ��): $n"
    $kept++
  }
}

Write-Host ""
if ($Delete) {
  Write-Host "��ɣ����� $kept ���ļ���ɾ�� $deleted ���ļ���"
} else {
  Write-Host "��ɣ����� $kept ���ļ������� $moved ���ļ� �� $removedDir��"
}
