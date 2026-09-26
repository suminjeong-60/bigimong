#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

npm test
python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py'
g++ -std=c++17 -Wall -Wextra -pedantic -O2 \
  tests/unreal_hatch_core_test.cpp -o /tmp/bigimong-unreal-hatch-core-test
/tmp/bigimong-unreal-hatch-core-test
