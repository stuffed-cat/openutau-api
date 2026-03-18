#!/bin/bash
set -e

# Clear old coverage results
rm -rf TestResults/ CoverageReport/

# We need this to ensure dotnet test doesn't run tests in parallel which messes up OpenUtau Singletons
# By using -maxthreads 1, we ensure sequential execution.
~/.dotnet/dotnet test OpenUtau.Api.Tests.csproj \
    --collect:"XPlat Code Coverage" \
    --settings coverlet.runsettings \
    -- -maxthreads 1

echo "Generating HTML report..."
# We explicitly set the .NET path so the global tool can find ASP.NET hosting elements
export DOTNET_ROOT=~/.dotnet
~/.dotnet/tools/reportgenerator \
    -reports:TestResults/*/coverage.cobertura.xml \
    -targetdir:CoverageReport \
    -reporttypes:Html

echo "Coverage report generated at: tests/OpenUtau.Api.Tests/CoverageReport/index.html"
