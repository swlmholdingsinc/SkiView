#!/bin/bash
# One editor session for scene + captures + rider shots + Android (FullPipeline), then WebGL. Waits out Mamta's jobs
# (Tools/mamta_busy.sh, self-excluding), load < 12, >= 6 GB free; runs at nice 15.
U=~/Unity/6000.3.26f1/Editor/Unity; P=/workspace/skiview-unity; cd $P
free_now() { ! bash $P/Tools/mamta_busy.sh >/dev/null && [ "$(awk '{print int($1)}' /proc/loadavg)" -lt 12 ] && [ "$(awk '/MemAvailable/{print int($2/1048576)}' /proc/meminfo)" -ge 6 ]; }
diskok() { local m=$(df -BM --output=avail / | tail -1 | tr -dc 0-9); [ "$m" -ge 8192 ] || { echo "$(date +%T) only ${m} MB free (< 8 GB): stopping before the build, nothing deleted"; exit 4; }; }
waitfree() { diskok; for i in $(seq 1 720); do if free_now; then sleep 20; free_now && { echo "$(date +%T) clear (load $(cut -d' ' -f1 /proc/loadavg))"; return; }; fi; sleep 10; done; echo "$(date +%T) still busy after 2 h, giving up"; exit 3; }
waitfree
T=$(date +%s); LIBGL_ALWAYS_SOFTWARE=1 nice -n 15 timeout 3600 xvfb-run -a -s "-screen 0 1920x1080x24" $U -batchmode -force-glcore -quit -projectPath $P -buildTarget Android -executeMethod SkiViewCapture.FullPipeline -logFile $P/Logs-pipeline.log; echo "pipeline(capture+rider+android) exit=$? $(( $(date +%s)-T ))s"
/workspace/skiview-tools-venv/bin/python Tools/make_rider_compare.py rider_compare_v6.png; /workspace/skiview-tools-venv/bin/python Tools/make_rider_closeup_compare.py rider_compare_closeup_v15.png; /workspace/skiview-tools-venv/bin/python Tools/make_env_compare.py env_compare_v17.png; /workspace/skiview-tools-venv/bin/python Tools/make_dws_fall_sheet.py dws_fall_v15.png; /workspace/skiview-tools-venv/bin/python Tools/make_jump_sheet.py jump_v15.png
/workspace/skiview-tools-venv/bin/python Tools/make_lake_sheet.py cedarbend lake_cedarbend_v18.png; /workspace/skiview-tools-venv/bin/python Tools/make_lake_sheet.py saltwhite lake_saltwhite_v18.png; for l in villaserena tallpine heronmarsh; do /workspace/skiview-tools-venv/bin/python Tools/make_lake_sheet.py $l lake_${l}_v18.png; done; /workspace/skiview-tools-venv/bin/python Tools/make_cameras_sheet.py cameras_v18.png; /workspace/skiview-tools-venv/bin/python Tools/make_boats_sheet.py boats_v18.png
waitfree
T=$(date +%s); nice -n 15 timeout 5400 $U -batchmode -nographics -quit -projectPath $P -buildTarget WebGL -executeMethod SkiViewBuild.WebGL -logFile $P/Logs-webgl.log; echo "webgl exit=$? $(( $(date +%s)-T ))s"
echo ALLDONE
