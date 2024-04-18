rem @ECHO off
rem nuget sign *.nupkg -CertificatePath %gs-nuget-certificate% -CertificatePassword "%gs-nuget-certificate-password%" -Timestamper http://sha256timestamp.ws.symantec.com/sha256/timestamp
nuget sign *.nupkg -Timestamper http://timestamp.digicert.com -CertificateFingerprint %SM_THUMBPRINT%