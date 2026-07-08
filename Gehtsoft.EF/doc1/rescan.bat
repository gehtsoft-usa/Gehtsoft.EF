@echo off
cd..
dotnet restore Gehtsoft.EF.sln
msbuild Gehtsoft.EF.sln /p:Configuration=Release
cd doc1
dotnet build project.proj /t:Scan,Prepare
