$existingService = Get-WmiObject -Class Win32_Service -Filter "Name='Hubot_MSGroupChatAdapterService'"

if ($existingService) 
{
  Write-Host "'Hubot_MSGroupChatAdapterService' exists already. Stopping..."
  Stop-Service Hubot_MSGroupChatAdapterService
  Write-Host "Waiting 3 seconds to allow existing service to stop..."
  Start-Sleep -s 3

  $existingService.Delete()
  Write-Host "Waiting 5 seconds to allow service to be uninstalled..."
  Start-Sleep -s 5  
}

Write-Host "Installing Hubot_MSGroupChatAdapterService..."
New-Service -Name Hubot_MSGroupChatAdapterService -BinaryPathName C:\Hubot-MSGroupChatAdapterService\Hubot-MSGroupChatAdapterService.exe -Description "Adapter/bridge between Microsoft GroupChat and Hubot" -DisplayName Hubot_MSGroupChatAdapterService -StartupType Automatic

Write-Host "Starting Hubot_MSGroupChatAdapterService..."
Start-Service -Name "Hubot_MSGroupChatAdapterService"

Write-Host "Done."
