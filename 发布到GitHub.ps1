# 发布 PCL 昔涟版到 GitHub
# 用法：右键「使用 PowerShell 运行」，或 pwsh -File 发布到GitHub.ps1
Set-Location "I:\YZ\0\PCL-CE"

Write-Host "1/3 检查状态..." -ForegroundColor Cyan
git status --short | Select-Object -First 5

Write-Host "2/3 提交..." -ForegroundColor Cyan
git add -A
git commit -m "PCL 昔涟版 V1.14.0" 2>$null

Write-Host "3/3 推送（会弹浏览器让你登录 GitHub）..." -ForegroundColor Cyan
git push -u origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "推送失败。常见原因：" -ForegroundColor Yellow
    Write-Host "  · 仓库还没建：先去 https://github.com/new 建一个叫 PCLCyrene 的 Public 仓库"
    Write-Host "  · 分支不叫 main：git branch -M main 再推"
    Write-Host "  · 没登录：弹窗里点 Sign in with your browser"
} else {
    Write-Host ""
    Write-Host "推送成功！下一步：" -ForegroundColor Green
    Write-Host "  Releases -> Draft a new release -> tag v1.14.0"
    Write-Host "  上传 PCLCyrene-Setup.exe 和 PCLCyrene-Update.zip"
}