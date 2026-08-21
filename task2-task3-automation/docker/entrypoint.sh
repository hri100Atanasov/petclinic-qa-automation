#!/usr/bin/env bash
set -uo pipefail

MODE="${1:-all}"
RESULTS_DIR="/app/testresults"
mkdir -p "$RESULTS_DIR"

run_ui() {
    echo ""
    echo "=== Running UI tests (Playwright) ==="
    dotnet test src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj \
        --no-build -c Release \
        --logger "trx;LogFileName=ui-results.trx" \
        --logger "html;LogFileName=ui-report.html" \
        --logger "console;verbosity=normal" \
        --results-directory "$RESULTS_DIR"
}

run_api() {
    echo ""
    echo "=== Running API tests (RestSharp) ==="
    dotnet test src/PetClinic.Tests.Api/PetClinic.Tests.Api.csproj \
        --no-build -c Release \
        --logger "trx;LogFileName=api-results.trx" \
        --logger "html;LogFileName=api-report.html" \
        --logger "console;verbosity=normal" \
        --results-directory "$RESULTS_DIR"
}

ui_status=0
api_status=0
run_ui_suite=false
run_api_suite=false

case "$MODE" in
    ui)
        run_ui_suite=true
        ;;
    api)
        run_api_suite=true
        ;;
    all)
        run_ui_suite=true
        run_api_suite=true
        ;;
    *)
        echo "Unknown mode '$MODE'. Usage: docker compose run --rm tests {ui|api|all}" >&2
        exit 2
        ;;
esac

# Both requested suites always run, even if one fails, so the summary below is
# always complete rather than stopping at the first failure.
if [ "$run_ui_suite" = true ]; then
    run_ui || ui_status=$?
fi
if [ "$run_api_suite" = true ]; then
    run_api || api_status=$?
fi

echo ""
echo "=== Summary ==="
if [ "$run_ui_suite" = true ]; then
    if [ "$ui_status" -eq 0 ]; then echo "UI suite:  PASSED"; else echo "UI suite:  FAILED"; fi
fi
if [ "$run_api_suite" = true ]; then
    if [ "$api_status" -eq 0 ]; then echo "API suite: PASSED"; else echo "API suite: FAILED"; fi
fi
echo "TRX + HTML reports written to ./testresults"

if [ "$ui_status" -ne 0 ] || [ "$api_status" -ne 0 ]; then
    exit 1
fi
exit 0
