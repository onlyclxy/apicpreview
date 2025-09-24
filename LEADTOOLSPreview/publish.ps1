# LEADTOOLS Preview .NET 8 发布脚本
# 自动发布单文件可执行程序，包含所有 LEADTOOLS 依赖

Write-Host "正在发布 LEADTOOLSPreview (.NET 8) ..." -ForegroundColor Green

# 清理之前的发布
Write-Host "清理之前的发布文件..." -ForegroundColor Yellow
if (Test-Path ".\publish") {
    Remove-Item ".\publish" -Recurse -Force
}

# 发布为单文件可执行程序
Write-Host "发布单文件应用程序..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ".\publish\single-file"

# 发布为框架依赖部署
Write-Host "发布框架依赖版本..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained false -o ".\publish\framework-dependent"

# 复制许可证文件
Write-Host "复制 LEADTOOLS 许可证文件..." -ForegroundColor Yellow
if (Test-Path ".\full_license.key") {
    Copy-Item ".\full_license.key" ".\publish\single-file\" -ErrorAction SilentlyContinue
    Copy-Item ".\full_license.key" ".\publish\framework-dependent\" -ErrorAction SilentlyContinue
}
if (Test-Path ".\full_license.lic") {
    Copy-Item ".\full_license.lic" ".\publish\single-file\" -ErrorAction SilentlyContinue
    Copy-Item ".\full_license.lic" ".\publish\framework-dependent\" -ErrorAction SilentlyContinue
}

# 复制额外的 LEADTOOLS 文件
Write-Host "复制 LEADTOOLS 运行时文件..." -ForegroundColor Yellow
$leadtoolsPath = "C:\LEADTOOLS 19\Bin"

# 复制必要的运行时文件
if (Test-Path "$leadtoolsPath\CDLL\x64") {
    $cdllPath = "$leadtoolsPath\CDLL\x64"
    Copy-Item "$cdllPath\*.dll" ".\publish\single-file\" -ErrorAction SilentlyContinue
    Copy-Item "$cdllPath\*.dll" ".\publish\framework-dependent\" -ErrorAction SilentlyContinue
}

Write-Host "发布完成！" -ForegroundColor Green
Write-Host "单文件版本: .\publish\single-file\LEADTOOLSPreview.exe" -ForegroundColor Cyan
Write-Host "框架依赖版本: .\publish\framework-dependent\LEADTOOLSPreview.exe" -ForegroundColor Cyan
Write-Host "注意：请确保目标机器已安装 .NET 8 桌面运行时（框架依赖版本）" -ForegroundColor Yellow

# 显示文件大小信息
if (Test-Path ".\publish\single-file\LEADTOOLSPreview.exe") {
    $singleFileSize = (Get-Item ".\publish\single-file\LEADTOOLSPreview.exe").Length / 1MB
    Write-Host "单文件大小: $([math]::Round($singleFileSize, 2)) MB" -ForegroundColor Cyan
}

pause 