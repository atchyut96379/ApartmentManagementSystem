# Sets SMS-related App Service settings for marvelrocks-ams.
# Usage:
#   .\scripts\Set-AzureSmsSettings.ps1 -Mode Simulation
#   .\scripts\Set-AzureSmsSettings.ps1 -Mode Msg91 -AuthKey "your-key" -SenderId "MRocks"

param(
    [ValidateSet("Simulation", "Msg91")]
    [string]$Mode = "Simulation",
    [string]$ResourceGroup = "rg-marvelrocks-ams",
    [string]$AppName = "marvelrocks-ams",
    [string]$AuthKey = "",
    [string]$SenderId = "MRocks"
)

$ErrorActionPreference = "Stop"

$settings = @{
    "Notification__EnableSms" = "true"
    "Notification__SmsProvider" = $Mode
}

if ($Mode -eq "Msg91") {
    if ([string]::IsNullOrWhiteSpace($AuthKey)) {
        throw "For Msg91 mode, pass -AuthKey from https://msg91.com → API"
    }
    $settings["Notification__Msg91AuthKey"] = $AuthKey.Trim()
    $settings["Notification__Msg91SenderId"] = $SenderId.Trim()
}

Write-Host "Updating App Service settings on $AppName ..."
$pairs = $settings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $AppName `
    --settings $pairs | Out-Null

Write-Host "Done. Restart the app:"
Write-Host "  az webapp restart --resource-group $ResourceGroup --name $AppName"
