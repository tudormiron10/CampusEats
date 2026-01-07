#!/bin/bash
# Coverage Report Generator for CampusEats
# Usage: ./scripts/coverage.sh

set -e

cd "$(dirname "$0")/.."

echo "Cleaning previous coverage results..."
rm -rf TestResults coveragereport

echo "Running tests with coverage collection..."
dotnet test --collect:"XPlat Code Coverage" --verbosity quiet

echo "Generating HTML report..."
reportgenerator \
  -reports:"**/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"Html;TextSummary;Badges"

echo ""
echo "Coverage report generated!"
echo "Open: coveragereport/index.html"
echo ""

# Show summary
if [ -f "coveragereport/Summary.txt" ]; then
  cat coveragereport/Summary.txt
fi