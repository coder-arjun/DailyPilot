# Builds the signed release APK and drops it at <repo>\build\DayPilot.apk.
# Usage: powershell -File mobile\scripts\build-apk.ps1  (from anywhere)
$ErrorActionPreference = 'Stop'
$mobile = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $mobile
Set-Location $mobile

if (-not (Test-Path "$mobile\google-services.json")) { throw 'google-services.json missing in mobile/ — Firebase Android app config required.' }
if (-not (Test-Path "$mobile\android-signing\daypilot-release.keystore")) { throw 'Release keystore missing (mobile/android-signing/).' }

# Parse the store/key password out of credentials.txt
$credText = Get-Content "$mobile\android-signing\credentials.txt" -Raw
$storePass = ([regex]::Match($credText, 'storePassword:\s*(\S+)')).Groups[1].Value
if (-not $storePass) { throw 'storePassword not found in credentials.txt' }

Write-Host '== expo prebuild (regenerates android/) =='
npx expo prebuild --platform android --clean --no-install
if ($LASTEXITCODE -ne 0) { throw 'prebuild failed' }

Write-Host '== patch signing config + ABI filter =='
# prebuild --clean wipes local.properties; the SDK on this machine lives here.
Set-Content "$mobile\android\local.properties" 'sdk.dir=C:/Android/sdk'
# ABIs: phones only (halves the APK vs all four)
Add-Content "$mobile\android\gradle.properties" "`nreactNativeArchitectures=armeabi-v7a,arm64-v8a"

$gradle = Get-Content "$mobile\android\app\build.gradle" -Raw
$signBlock = @"
        release {
            storeFile file('$($mobile -replace '\\','/')/android-signing/daypilot-release.keystore')
            storePassword '$storePass'
            keyAlias 'daypilot'
            keyPassword '$storePass'
        }
"@
# Insert a release signing config after the debug one, then point the release build type at it.
$gradle = $gradle -replace '(?s)(signingConfigs\s*\{.*?debug\s*\{.*?\n        \})', "`$1`n$signBlock"
$gradle = $gradle -replace '(?s)(buildTypes\s*\{.*?release\s*\{.*?)signingConfig signingConfigs\.debug', '$1signingConfig signingConfigs.release'
Set-Content "$mobile\android\app\build.gradle" $gradle -NoNewline

if ((Get-Content "$mobile\android\app\build.gradle" -Raw) -notmatch 'signingConfigs\.release') { throw 'signing patch failed — inspect android/app/build.gradle' }

Write-Host '== gradle assembleRelease =='
Set-Location "$mobile\android"
.\gradlew.bat assembleRelease --no-daemon
if ($LASTEXITCODE -ne 0) { throw 'gradle build failed' }

$apk = "$mobile\android\app\build\outputs\apk\release\app-release.apk"
if (-not (Test-Path $apk)) { throw "APK not found at $apk" }
New-Item -ItemType Directory -Force "$repo\build" | Out-Null
Copy-Item $apk "$repo\build\DayPilot.apk" -Force
Write-Host "== DONE: $repo\build\DayPilot.apk ($([math]::Round((Get-Item "$repo\build\DayPilot.apk").Length / 1MB, 1)) MB) =="