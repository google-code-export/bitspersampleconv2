cd bin\x64\Release
copy ..\..\..\locbaml_x64.exe locbaml.exe
rem locbaml /parse en-US\PlayPcmWin.resources.dll /out:..\..\..\PlayPcmWinx64.en-US.csv
locbaml /parse en-US\PlayPcmWin.resources.dll /out:..\..\..\PlayPcmWin.en-US.csv
del locbaml.exe
cd ..\..\..

pause

