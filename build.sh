#!/bin/bash
echo 'Building Environment'

# If WiiBalanceWalker isn't installed, try to install it
if [ ! -d "WiiBalanceWalker" ]; then
  read -p "Directory WiiBalanceWalker does not exist. Install from GitHub (https://github.com/lshachar/WiiBalanceWalker).Y/N" install
  if [[ $install == [yY] ]]; then
    git clone https://github.com/lshachar/WiiBalanceWalker
  fi
fi

echo 'Moving Files'
cp -f FormMain_edit.cs WiiBalanceWalker/FormMain.cs
cp -f WiiBalanceWalker_edit.csproj WiiBalanceWalker/WiiBalanceWalker.csproj

cp -f Data.cs WiiBalanceWalker/Data.cs

echo 'Building'

# Assumes .NET msbuild is here
'C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe' ./WiiBalanceWalker.sln

echo 'Done!'
read -p "Do you want to run the program? Y/N" run
if [[ $run == [yY] ]]; then
  WiiBalanceWalker/bin/x64/Debug/WiiBalanceWalker.exe
fi
