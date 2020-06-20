#!/bin/bash -xe
HERE=$(dirname $0)

dotnet restore
dotnet build -c Release --runtime ubuntu.18.04-x64
cp $HERE/NUnitDotNetCoreRunner/bin/Release/* $HERE/../../bzt/resources/NUnitDotNetCoreRunner/