@echo off
dotnet build project.proj /t:CleanDoc,Scan,Prepare,MakeDoc
