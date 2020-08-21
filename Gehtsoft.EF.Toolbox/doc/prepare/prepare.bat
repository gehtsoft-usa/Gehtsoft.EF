@echo off
if exist dst del dst\*.* /q /s > nul
if not exist dst md dst
if exist src del src\*.* /q /s > nul
if not exist src md src
"%sandcastle%\ProductionTools\MrefBuilder.exe" ..\..\Gehtsoft.EF.Mapper\bin\Debug\net461\Gehtsoft.Mapper.dll ..\..\Gehtsoft.EF.Mapper\bin\Release\net461\Gehtsoft.EF.Mapper.dll ..\..\Gehtsoft.Validator\bin\Release\net461\Gehtsoft.Validator.dll  /out:doc-source.xml
if not exist src mkdir src
%docgen%\bin\docgen prepare.xml
