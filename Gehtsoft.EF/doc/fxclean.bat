@echo off
if exist dst del dst\*.* /q /s
del Gehtsoft.Db.xml
del Gehtsoft.Entities.xml
if exist prepare (
    cd prepare
    call clear.bat
    cd ..
)