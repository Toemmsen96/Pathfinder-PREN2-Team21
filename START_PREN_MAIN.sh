#!/bin/bash
export DOTNET_ROOT=/home/pren/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
cd /home/pren/pfadfinder
./PfadfinderMain
