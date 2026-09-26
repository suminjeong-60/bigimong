#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

npm test
python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py'
python3 -m unittest discover -s tests -p 'test_prepare_unreal_male.py'
python3 -m unittest discover -s tests -p 'test_unreal_android_build.py'
g++ -std=c++17 -Wall -Wextra -pedantic -O2 \
  tests/unreal_hatch_core_test.cpp -o /tmp/bigimong-unreal-hatch-core-test
/tmp/bigimong-unreal-hatch-core-test
g++ -std=c++17 -Wall -Wextra -pedantic -O2 \
  tests/unreal_male_avatar_test.cpp -o /tmp/bigimong-unreal-male-avatar-test
/tmp/bigimong-unreal-male-avatar-test
