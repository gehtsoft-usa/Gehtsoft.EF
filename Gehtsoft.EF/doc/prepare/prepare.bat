@echo off
if not exist src mkdir src
if not exist src\null.ds copy /b nul src\null.ds
dotnet build project.proj /t:Scan,Raw