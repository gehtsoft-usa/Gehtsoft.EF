@echo off
nuget push %1 -ApiKey %ng-nuget-api-key% -Source https://api.nuget.org/v3/index.json
