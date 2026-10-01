#!/bin/zsh
set -u

script_dir="${0:A:h}"
for batch in {01..10}; do
  "$script_dir/run_batch.sh" "$batch" || exit $?
done
echo "ALL 10 GAME VALIDATION BATCHES PASSED"
