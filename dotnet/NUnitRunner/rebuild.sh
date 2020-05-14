#!/bin/bash -xe
HERE=$(dirname $0)

dotnet restore
dotnet build --runtime ubuntu.18.04-x64
cp $HERE/NUnitRunner/bin/Release/* $HERE/../../bzt/resources/NUnitRunner/