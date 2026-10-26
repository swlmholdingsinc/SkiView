#!/bin/bash
# Exit 0 (and print the matches) if any of Mamta's jobs are running: PoliticalKombat/Filibuster builds, PlayMode -runTests,
# FilibusterFighters screenshot/QA players, pk_run wrappers, Blender jobs, or ANY batchmode Unity not on skiview-unity.
# Patterns use the [x]bc bracket trick, and this script's own PID tree is excluded, so the check can't match itself.
self=$$; parent=$PPID
m=$(ps -eo pid=,args= | awk -v s="$self" -v p="$parent" '$1!=s && $1!=p' \
  | grep -E '[Pp]olitical[-_ ]?[Kk]ombat|[Ff]ilibuster|[p]k_run|[-]pkOut|[-]pkScreens|[b]lender|[U]nity.*-batchmode|[U]nity.*-runTests' \
  | grep -v -e 'skiview-unity' -e '[m]amta_busy' -e '[g]rep -E')
if [ -n "$m" ]; then echo "$m" | cut -c1-160; exit 0; fi
exit 1
