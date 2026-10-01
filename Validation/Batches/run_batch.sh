#!/bin/zsh
set -u

if [[ $# -ne 1 || ! "$1" =~ '^(0[1-9]|10)$' ]]; then
  echo "Usage: $0 <01..10>"
  exit 2
fi

batch="$1"
project_dir="${0:A:h:h:h}"
unity="/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity"
log_dir="$project_dir/Validation/BatchLogs"
log_file="$log_dir/batch_${batch}.log"

mkdir -p "$log_dir"
if pgrep -f "Unity.*-projectPath $project_dir" >/dev/null; then
  echo "ERROR: Close the Unity Editor for this project before running validation batches."
  exit 3
fi

echo "Running validation batch $batch..."
"$unity" -batchmode -nographics -quit -projectPath "$project_dir" \
  -executeMethod "GameValidationBatches.Batch${batch}" -logFile "$log_file"
exit_code=$?

if [[ $exit_code -eq 0 ]] && grep -q "GAME_VALIDATION_BATCH_${batch}_PASSED" "$log_file"; then
  echo "PASS batch $batch — $log_file"
  exit 0
fi

echo "FAIL batch $batch — $log_file"
tail -40 "$log_file"
exit 1
