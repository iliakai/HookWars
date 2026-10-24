param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$playerPath = Join-Path $projectPath 'Builds\WindowsDevelopment\HookWars.exe'
if (!(Test-Path -LiteralPath $playerPath)) { throw 'Build HookWars.exe before running this check.' }
if (Get-Process HookWars -ErrorAction SilentlyContinue) { throw 'Close HookWars before running the automated player check.' }
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $projectPath ('Temp\WindowsSmoke-' + [guid]::NewGuid().ToString('N'))
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Unity persists command-line window overrides in the current user's registry.
# Preserve only display preferences; leave gameplay and Unity service data alone.
$registryPath = 'Software\DefaultCompany\HookWars'
function Test-DisplayPreference([string]$Name) {
    return $Name.StartsWith('Screenmanager ') -or $Name.StartsWith('UnitySelectMonitor_')
}
$savedDisplay = @()
$settings = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryPath)
if ($settings) {
    try {
        foreach ($name in $settings.GetValueNames()) {
            if (Test-DisplayPreference $name) {
                $savedDisplay += [pscustomobject]@{
                    Name = $name; Value = $settings.GetValue($name); Kind = $settings.GetValueKind($name)
                }
            }
        }
    } finally { $settings.Dispose() }
}

$player = $null
try {
    $arguments = @('-hookwars-smoke', ('"' + $OutputDirectory + '"'),
        '-logFile', ('"' + (Join-Path $OutputDirectory 'player.log') + '"'),
        '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0')
    $player = Start-Process -FilePath $playerPath -ArgumentList $arguments `
        -WorkingDirectory (Split-Path -Parent $playerPath) -WindowStyle Hidden -PassThru
    Write-Output "Windows smoke output: $OutputDirectory"
    if (!$player.WaitForExit(120000)) { throw 'Windows player check timed out.' }
    if ($player.ExitCode -ne 0) { throw "Windows player exited with code $($player.ExitCode)." }
    $resultPath = Join-Path $OutputDirectory 'result.txt'
    if (!(Select-String -LiteralPath $resultPath -Pattern '^HOOKWARS_WINDOWS_SMOKE_PASS:' -Quiet)) {
        throw 'Windows player did not report successful acceptance checks.'
    }
    Get-Content -LiteralPath $resultPath | Select-Object -Last 1
} finally {
    # Wait until our test process stops before restoring values it writes on exit.
    if ($player -and !$player.HasExited) { $player.Kill(); $player.WaitForExit() }
    $settings = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($registryPath)
    try {
        foreach ($name in $settings.GetValueNames()) {
            if (Test-DisplayPreference $name) { $settings.DeleteValue($name, $false) }
        }
        foreach ($entry in $savedDisplay) { $settings.SetValue($entry.Name, $entry.Value, $entry.Kind) }
    } finally { $settings.Dispose() }
    Write-Output 'Display preferences restored.'
}
