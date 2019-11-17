#!/usr/bin/env bash

set -eu
set -o pipefail

mono dotnet tool install -g fake-cli
mono fake.exe "$@"
