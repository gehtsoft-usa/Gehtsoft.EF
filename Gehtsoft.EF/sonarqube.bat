dotnet sonarscanner begin /k:"Gehtsoft.EF" /d:sonar.host.url="http://localhost:9000"  /d:sonar.login="233a442e3701582a3e1b786872080484cb995ff0"
dotnet build Gehtsoft.EF.sln
dotnet sonarscanner end /d:sonar.login="233a442e3701582a3e1b786872080484cb995ff0"