dotnet sonarscanner begin /k:"Gehtsoft.EF" /d:sonar.host.url="http://localhost:9000"  /d:sonar.login="a8e0fc5d912b67de85ecbdf3243baba0ac2c61d3"
dotnet build Gehtsoft.EF.sln
dotnet sonarscanner end /d:sonar.login="a8e0fc5d912b67de85ecbdf3243baba0ac2c61d3"