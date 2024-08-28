mkdir build
rm bin/Release/net8.0-windows/win-x64/publish/AIPG-Omniworker-Windows-Installer.exe
dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
cp bin/Release/net8.0-windows/win-x64/publish/AIPG-Omniworker-Windows-Installer.exe build/AIPG-Omniworker-Windows-Installer.exe
echo ""
echo ""
echo "If build succeeded, the output is in build/AIPG-Omniworker-Windows-Installer.exe"
echo "Press any key to exit..."
read