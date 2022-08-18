cd..
dotnet restore Gehtsoft.EF.sln
msbuild Gehtsoft.EF.sln /p:Configuration=Release
cd nuget
msbuild nuget.proj -t:Prepare
msbuild nuget.proj -t:NuSpec,NuPack
