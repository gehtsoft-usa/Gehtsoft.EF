@echo off
if exist dst del dst\*.* /q /s
del Gehtsoft.EF.Mapper.xml
if exist prepare (
    cd prepare
    call clear.bat
    cd ..
)