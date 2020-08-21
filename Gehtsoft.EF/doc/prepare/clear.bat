@echo off
if exist dst del dst\*.* /q /s > nul
if exist src del src\*.* /q /s > nul
if exist doc-source.xml del doc-source.xml