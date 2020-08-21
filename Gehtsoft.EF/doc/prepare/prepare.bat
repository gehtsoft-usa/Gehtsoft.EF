@echo off
if exist dst del dst\*.* /q /s > nul
if not exist dst md dst
if exist src del src\*.* /q /s > nul
if not exist src md src
"%sandcastle%\ProductionTools\MrefBuilder.exe" ..\..\Gehtsoft.Ef.Db.SqlDb\bin\Release\net461\Gehtsoft.Ef.Entities.dll ..\..\Gehtsoft.Ef.Db.SqlDb\bin\Release\net461\Gehtsoft.Ef.Db.Sqldb.dll ..\..\Gehtsoft.Ef.FTS\bin\Release\net461\Gehtsoft.Ef.FTS.dll ..\..\Gehtsoft.EF.Db.SqlDb.OData\bin\Release\net461\Gehtsoft.EF.Db.SqlDb.OData.dll  /out:doc-source.xml
if not exist src mkdir src
%docgen%\bin\docgen prepare.xml
