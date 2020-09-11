@rem if anything went wrong make sure that packages are restored: nuget restore TespApp.csproj
msbuild TestWebApp.csproj -t:CleanContent,Content