@echo off
if exist .git rmdir .git
if not exist ef mkdir ef
if not exist ef.toolbox mkdir ef.toolbox
del ef\*.* /q /s
del ef.toolbox\*.* /q /s
cd ..\Gehtsoft.EF\doc1
call doc.bat
xcopy .\dst ..\..\packagedoc\ef /s /y
cd ..\..\Gehtsoft.EF.Toolbox\doc
call doc.bat
xcopy .\dst ..\..\packagedoc\ef.toolbox /s /y