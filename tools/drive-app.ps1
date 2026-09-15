# Drive the app through UI Automation: open a cartridge, point the output at a scratch folder,
# press Build, wait, capture the window. No synthetic mouse or keyboard input.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Cartridge,
    [Parameter(Mandatory = $true)][string]$OutputFolder,
    [Parameter(Mandatory = $true)][string]$Shot
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing

Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public static class W {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int Left, Top, Right, Bottom; }
}
"@

function Get-Root($procId) {
    for ($i = 0; $i -lt 40; $i++) {
        $c = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
        $e = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children, $c)
        if ($e) { return $e }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Find-ByName($root, $name, $type) {
    $and = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)))
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}

function Save-Shot($hwnd, $path) {
    $r = New-Object W+R
    [W]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc(); [W]::PrintWindow($hwnd, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

$proc = Start-Process -FilePath $Exe -ArgumentList "`"$Cartridge`"" -PassThru
$root = Get-Root $proc.Id
if (-not $root) { Write-Output "no automation root"; $proc.Kill(); exit 1 }
Start-Sleep -Seconds 4

# The output folder box is the one holding a path under Documents; set it to the scratch folder.
$edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Edit)))
Write-Output "edit boxes: $($edits.Count)"
foreach ($e in $edits) {
    $vp = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) {
        if ($vp.Current.Value -like "*LMS 2 Website\sites\*") {
            $vp.SetValue($OutputFolder)
            Write-Output "output folder set to $OutputFolder"
        }
    }
}

$build = Find-ByName $root "Build website" ([System.Windows.Automation.ControlType]::Button)
if (-not $build) { Write-Output "Build button not found"; $proc.Kill(); exit 1 }
$ip = $build.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$ip.Invoke()
Write-Output "build invoked"

Start-Sleep -Seconds 30

# Scroll the page to the bottom so the result and step 3 are in the shot.
$sv = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Pane)))
$scroll = $null
if ($root.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$scroll)) {
    $scroll.SetScrollPercent(-1, 100)
} else {
    foreach ($e in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)) {
        $sp = $null
        if ($e.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$sp)) {
            if ($sp.Current.VerticallyScrollable) { $sp.SetScrollPercent(-1, 100); break }
        }
    }
}
Start-Sleep -Milliseconds 800
$proc.Refresh()
Save-Shot ([IntPtr]$root.Current.NativeWindowHandle) $Shot
Write-Output "saved $Shot"
if (-not $proc.HasExited) { $proc.Kill() }
