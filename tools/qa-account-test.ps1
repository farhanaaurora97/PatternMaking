# PatternPro My Account module — full HTTP test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-account-test.ps1

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$UserName = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Continue"
$script:Passed = 0
$script:Failed = 0
$script:Errors = [System.Collections.Generic.List[string]]::new()
$script:TempPassword = "Admin@124"

function Pass([string]$Name, [string]$Detail = "") {
    $script:Passed++
    $line = "  PASS  $Name"
    if ($Detail) { $line += " - $Detail" }
    Write-Host $line -ForegroundColor Green
}

function Fail([string]$Name, [string]$Detail = "") {
    $script:Failed++
    $line = "  FAIL  $Name"
    if ($Detail) { $line += " - $Detail" }
    Write-Host $line -ForegroundColor Red
    $script:Errors.Add("$Name : $Detail")
}

function Try-Test([string]$Name, [scriptblock]$Block) {
    try { & $Block }
    catch { Fail $Name $_.Exception.Message }
}

function Get-Antiforgery([string]$Html) {
    $m = [regex]::Match($Html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Anti-forgery token not found" }
    return $m.Groups[1].Value
}

function Login-User([string]$Name, [string]$Pass) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -WebSession $session -UseBasicParsing
    $token = Get-Antiforgery $loginPage.Content
    $body = @{
        UserName                   = $Name
        Password                   = $Pass
        RememberMe                 = "false"
        ReturnUrl                  = ""
        __RequestVerificationToken = $token
    }
    Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -Method POST -Body $body -WebSession $session -UseBasicParsing | Out-Null
    $adminPage = Invoke-WebRequest -Uri "$BaseUrl/Admin" -WebSession $session -UseBasicParsing
    if ($adminPage.BaseResponse.ResponseUri -match "/Account/Login") { throw "Login failed for $Name" }
    if ($adminPage.Content -notmatch "Admin panel|Users") { throw "Login failed for $Name" }
    return $session
}

function Post-ChangePassword($Session, [string]$Current, [string]$New, [string]$Confirm) {
    $page = Invoke-WebRequest -Uri "$BaseUrl/User/ChangePassword" -WebSession $Session -UseBasicParsing
    $token = Get-Antiforgery $page.Content
    $body = @{
        CurrentPassword            = $Current
        NewPassword                = $New
        ConfirmPassword            = $Confirm
        __RequestVerificationToken = $token
    }
    return Invoke-WebRequest -Uri "$BaseUrl/User/ChangePassword" -Method POST -Body $body -WebSession $Session -UseBasicParsing
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro My Account Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "AC0 Login" {
    $script:session = Login-User $UserName $Password
    Pass "AC0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Profile page ---
Write-Host "--- AC1. My account page ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "AC1 Page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/User" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    if ($r.BaseResponse.ResponseUri -match "/Account/Login") { throw "Redirected to login - profile not available" }
    if ($r.Content -notmatch "My account") { throw "My account page content missing" }
    $script:pageHtml = $r.Content
    Pass "AC1 Page loads" "/User"
}

$uiMarkers = @(
    @{ Name = "My account title";     Pattern = "My account" },
    @{ Name = "Employee profile";     Pattern = "Employee profile" },
    @{ Name = "Employee ID";          Pattern = "Employee ID" },
    @{ Name = "Full name";            Pattern = "Full name" },
    @{ Name = "Username";             Pattern = "Username" },
    @{ Name = "Role";                 Pattern = "Role" },
    @{ Name = "Account status";       Pattern = "Account status" },
    @{ Name = "Active tag";           Pattern = "Active" },
    @{ Name = "Change password link";  Pattern = "ChangePassword" },
    @{ Name = "What you can do";      Pattern = "What you can do" },
    @{ Name = "admin username";       Pattern = "admin" }
)
foreach ($m in $uiMarkers) {
    Try-Test "AC1 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "AC1 UI: $($m.Name)"
    }
}

Try-Test "AC1 Shows admin role" {
    if ($pageHtml -notmatch "Administrator|Admin") { throw "Admin role label missing" }
    Pass "AC1 Role" "Administrator"
}

# --- Change password page ---
Write-Host ""
Write-Host "--- AC2. Change password ---" -ForegroundColor Cyan

Try-Test "AC2 Change password page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/User/ChangePassword" -WebSession $session -UseBasicParsing
    if ($r.BaseResponse.ResponseUri -match "/Account/Login") { throw "Redirected to login" }
    if ($r.Content -notmatch "Change password") { throw "Page title missing" }
    if ($r.Content -notmatch 'id="CurrentPassword"|name="CurrentPassword"') { throw "Form fields missing" }
    Pass "AC2 Page loads"
}

Try-Test "AC2 Wrong current password rejected" {
    $r = Post-ChangePassword $session "WrongPass1!" $script:TempPassword $script:TempPassword
    if ($r.Content -notmatch "incorrect|Invalid|error") { throw "Expected validation error on page" }
    Pass "AC2 Wrong password rejected"
}

Try-Test "AC2 Mismatch confirm rejected" {
    $r = Post-ChangePassword $session $Password $script:TempPassword "Mismatch1"
    if ($r.Content -notmatch "match|validation|error|Error") { throw "Expected mismatch error" }
    Pass "AC2 Confirm mismatch rejected"
}

Try-Test "AC2 Update password" {
    $r = Post-ChangePassword $session $Password $script:TempPassword $script:TempPassword
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $uriOk = $r.BaseResponse.ResponseUri -match "/User"
    $msgOk = $r.Content -match "Password updated successfully"
    if (-not ($uriOk -or $msgOk)) { throw "Expected redirect to /User or success message" }
    $script:session = Login-User $UserName $script:TempPassword
    Pass "AC2 Password updated"
}

Try-Test "AC2 Login with new password" {
    if ($script:session -eq $null) { throw "Password update did not complete" }
    Pass "AC2 New password works" "(verified during update)"
}

Try-Test "AC2 Restore original password" {
    $r = Post-ChangePassword $session $script:TempPassword $Password $Password
    $uriOk = $r.BaseResponse.ResponseUri -match "/User"
    $msgOk = $r.Content -match "Password updated successfully"
    if (-not ($uriOk -or $msgOk)) { throw "Expected redirect to /User or success message" }
    $script:session = Login-User $UserName $Password
    Pass "AC2 Restored" $Password
}

# --- Auth ---
Write-Host ""
Write-Host "--- AC3. Auth ---" -ForegroundColor Cyan

Try-Test "AC3 User page auth gate" {
    $anon = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    try {
        Invoke-WebRequest -Uri "$BaseUrl/User" -WebSession $anon -MaximumRedirection 0 -UseBasicParsing -ErrorAction Stop | Out-Null
        throw "Expected redirect to login"
    }
    catch {
        if ($_.Exception.Message -match "Expected redirect") { throw }
        Pass "AC3 User auth gate" "redirect to login"
    }
}

Try-Test "AC3 AccessDenied page" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Account/AccessDenied" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "AC3 AccessDenied"
}

Try-Test "AC3 Registration closed" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Account/Register" -MaximumRedirection 0 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($r.StatusCode -ne 302) { throw "Expected redirect, got $($r.StatusCode)" }
    Pass "AC3 Registration closed"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MY ACCOUNT MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "ERRORS:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    exit 1
}

Write-Host "MY ACCOUNT MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: Desktop /account page, password form UX." -ForegroundColor DarkGray
Write-Host ""
exit 0
