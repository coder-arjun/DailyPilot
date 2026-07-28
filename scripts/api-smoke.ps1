# DayPilot API smoke test — auth round-trip + tasks CRUD.
# Usage: .\scripts\api-smoke.ps1 [-BaseUrl http://localhost:5245]
param([string]$BaseUrl = 'http://localhost:5245')

$ErrorActionPreference = 'Stop'
$api = "$BaseUrl/api/v1"
$pass = 0; $fail = 0

function Step($name, [scriptblock]$body) {
    try { & $body; $script:pass++; Write-Host "PASS  $name" }
    catch { $script:fail++; Write-Host "FAIL  $name -> $($_.Exception.Message)" }
}

function PostJson($url, $obj, $token = $null) {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    Invoke-RestMethod -Uri $url -Method Post -ContentType 'application/json' -Headers $headers -Body ($obj | ConvertTo-Json)
}

function GetJson($url, $token) {
    Invoke-RestMethod -Uri $url -Headers @{ Authorization = "Bearer $token" }
}

$n = Get-Random
$user = @{ displayName = 'Api Smoke'; userName = "apismoke$n"; email = "apismoke$n@test.local"; timeZoneId = 'India Standard Time'; password = 'ApiSmoke!2026x' }

$reg = $null
Step 'register returns tokens' {
    $script:reg = PostJson "$api/auth/register" $user
    if (-not $reg.accessToken -or -not $reg.refreshToken) { throw 'missing tokens' }
    if ($reg.user.userName -ne $user.userName) { throw 'wrong user' }
}

$login = $null
Step 'login returns tokens' {
    $script:login = PostJson "$api/auth/login" @{ login = $user.userName; password = $user.password }
    if (-not $login.accessToken) { throw 'missing access token' }
}

Step 'login works with email too' {
    $r = PostJson "$api/auth/login" @{ login = $user.email; password = $user.password }
    if (-not $r.accessToken) { throw 'missing access token' }
}

Step 'wrong password is 401' {
    try { PostJson "$api/auth/login" @{ login = $user.userName; password = 'nope' }; throw 'expected 401' }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
}

$rotated = $null
Step 'refresh rotates the token' {
    $script:rotated = PostJson "$api/auth/refresh" @{ refreshToken = $login.refreshToken }
    if (-not $rotated.accessToken -or $rotated.refreshToken -eq $login.refreshToken) { throw 'not rotated' }
}

Step 'old refresh token is dead (401)' {
    try { PostJson "$api/auth/refresh" @{ refreshToken = $login.refreshToken }; throw 'expected 401' }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
}

$token = $rotated.accessToken

Step 'unauthenticated tasks request is 401' {
    try { Invoke-RestMethod -Uri "$api/tasks"; throw 'expected 401' }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
}

$task = $null
Step 'create task' {
    $script:task = PostJson "$api/tasks" @{ title = 'Smoke task'; plannedDate = (Get-Date -Format 'yyyy-MM-dd'); priority = 1 } $token
    if (-not $task.id) { throw 'no id' }
}

Step 'today list contains the task' {
    $list = GetJson "$api/tasks" $token
    if (-not ($list | Where-Object id -eq $task.id)) { throw 'task missing from list' }
}

Step 'complete task' {
    PostJson "$api/tasks/$($task.id)/complete" @{} $token | Out-Null
    $t = GetJson "$api/tasks/$($task.id)" $token
    if (-not $t.isCompleted) { throw 'not completed' }
}

Step 'categories list' {
    GetJson "$api/categories" $token | Out-Null
}

Step 'delete task -> 404 on get' {
    Invoke-RestMethod -Uri "$api/tasks/$($task.id)" -Method Delete -Headers @{ Authorization = "Bearer $token" } | Out-Null
    try { GetJson "$api/tasks/$($task.id)" $token; throw 'expected 404' }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
}

Step 'logout revokes refresh token' {
    PostJson "$api/auth/logout" @{ refreshToken = $rotated.refreshToken } | Out-Null
    try { PostJson "$api/auth/refresh" @{ refreshToken = $rotated.refreshToken }; throw 'expected 401' }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
}

Write-Host ''
Write-Host "Result: $pass passed, $fail failed"
if ($fail -gt 0) { exit 1 }
