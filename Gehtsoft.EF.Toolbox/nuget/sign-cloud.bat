rem @ECHO off
forfiles /m *.nupkg /c "cmd /c smctl sign --simple --keypair-alias key_525680920 --input @path"
