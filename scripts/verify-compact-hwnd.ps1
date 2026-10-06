# Verify compact rest HWND equals the pill (no dual-geometry off-screen host).
# Rerun: powershell -NoProfile -File scripts/verify-compact-hwnd.ps1
$ErrorActionPreference = "Stop"
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) "DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe"
if (-not (Test-Path $exe)) { throw "Missing $exe. Build Release first." }

Get-Process -Name DeezFuelGauge -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$env:DEEZ_COMPACT_TRACE = "1"
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinEnum2 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

$pidTarget = $proc.Id
$script:found = @()
[WinEnum2]::EnumWindows({
  param($h,$l)
  [uint32]$p = 0
  [WinEnum2]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
  if ($p -eq $pidTarget -and [WinEnum2]::IsWindowVisible($h)) {
    $r = New-Object WinEnum2+RECT
    [WinEnum2]::GetWindowRect($h, [ref]$r) | Out-Null
    $sb = New-Object System.Text.StringBuilder 256
    [WinEnum2]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString() -match "Cursor|Fuel|Deez") {
      $script:found += [pscustomobject]@{
        Title=$sb.ToString(); Left=$r.Left; Top=$r.Top; W=($r.Right-$r.Left); H=($r.Bottom-$r.Top)
      }
    }
  }
  return $true
}, [IntPtr]::Zero) | Out-Null

if ($script:found.Count -eq 0) {
  Stop-Process -Id $pidTarget -Force -ErrorAction SilentlyContinue
  throw "No visible DeezFuelGauge window found."
}

$win = $script:found | Sort-Object W -Descending | Select-Object -First 1
Write-Host ("HWND Title={0} Left={1} Top={2} W={3} H={4}" -f $win.Title,$win.Left,$win.Top,$win.W,$win.H)

$failures = @()
if ($win.Top -lt -40) { $failures += "Top=$($win.Top) still parks the host above the screen." }
if ($win.Top -lt 0 -and $win.W -ge 250 -and $win.H -ge 200) {
  $failures += "Full-size host with Top=$($win.Top) is the dual-geometry failure mode."
}
if ($win.W -lt 40 -or $win.H -lt 20) { $failures += "Window too small to be the pill." }

Stop-Process -Id $pidTarget -Force -ErrorAction SilentlyContinue

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Host "FAIL: $_" }
  exit 1
}

Write-Host "PASS: HWND is on-screen and not the dual-geometry off-screen host."
exit 0
