@echo off
nuget push %1 -ApiKey %gs_proget_key% -Source https://proget.gehtsoft.com/nuget/public-nuget/v3/index.json
