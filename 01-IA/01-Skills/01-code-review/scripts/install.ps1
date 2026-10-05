<#
  Installs this skill as `ats:code-review` by linking it into the ats plugin folder.
  The source of truth stays in the repo; the link lets Claude Code discover it.

  Usage:  powershell -File scripts\install.ps1 [-Target <path>] [-Remove]
#>
param(
  [string]$Target = (Join-Path $HOME ".claude\skills\ats\skills\code-review"),
  [switch]$Remove
)
$ErrorActionPreference = "Stop"
$src = Split-Path -Parent $PSScriptRoot

if ($Remove) {
  if (Test-Path $Target) {
    $item = Get-Item $Target -Force
    if ($item.LinkType -ne "Junction") { throw "$Target is not a junction; refusing to delete it." }
    $item.Delete()
    Write-Host "Removed link $Target"
  }
  return
}

$parent = Split-Path -Parent $Target
if (-not (Test-Path $parent)) { throw "Plugin folder not found: $parent (is the ats plugin installed?)" }
if (Test-Path $Target) {
  $item = Get-Item $Target -Force
  if ($item.LinkType -eq "Junction" -and ($item.Target -contains $src)) { Write-Host "Already installed: $Target"; }
  else { throw "$Target already exists and is not a link to this skill. Move it or use -Target." }
} else {
  New-Item -ItemType Junction -Path $Target -Value $src | Out-Null
  Write-Host "Linked $Target -> $src"
}
python (Join-Path $src "review.py") validate
Write-Host "Restart Claude Code (or /reload-plugins) and invoke /ats:code-review"
