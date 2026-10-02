#!/bin/zsh
set -u

script_dir="${0:A:h}"
for batch in {01..20}; do
  "$script_dir/run_batch.sh" "$batch" || exit $?
done
echo "ALL 20 GAME VALIDATION BATCHES PASSED"
