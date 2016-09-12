$fqdn = (Get-WmiObject win32_computersystem).DNSHostName+"."+(Get-WmiObject win32_computersystem).Domain
$port = 5986

$webRequest = [Net.WebRequest]::Create("https://${fqdn}:${port}/wsman")
try { $webRequest.GetResponse() } catch {}
$cert = $webRequest.ServicePoint.Certificate

"-----BEGIN CERTIFICATE-----" | Out-File cert.pem -Encoding ascii
[Convert]::ToBase64String($cert.Export('cert'), 'InsertLineBreaks') |
  Out-File .\cert.pem -Append -Encoding ascii
  "-----END CERTIFICATE-----" | Out-File cert.pem -Encoding ascii -Append

  Write-Host "Cert file cert.pem created."