cd ..
dotnet restore Gehtsoft.EF.Toolbox.sln
msbuild Gehtsoft.EF.Toolbox.sln -p:Configuration=Release
cd nuget
msbuild nuget.proj -t:Prepare
msbuild nuget.proj -t:NuSpec,NuPack
