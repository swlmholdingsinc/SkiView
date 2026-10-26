#!/bin/bash
# Waits for other users' Unity builds to finish (never touches them), then rebuilds scene+screens, Android, WebGL.
U=~/Unity/6000.3.26f1/Editor/Unity; P=/workspace/skiview-unity; cd $P
waitfree() { sleep 5;  for i in $(seq 1 720); do if ! bash $P/Tools/mamta_busy.sh >/dev/null && [ "$(awk '{print int($1)}' /proc/loadavg)" -lt 12 ] && [ "$(awk '/MemAvailable/{print int($2/1048576)}' /proc/meminfo)" -ge 6 ]; then echo "$(date +%T) clear (load $(cut -d" " -f1 /proc/loadavg))"; return; fi; sleep 10; done; echo "$(date +%T) still busy after 120 min, giving up (not competing with other builds)"; exit 3; }
waitfree
[ -z "$SKIP_CAPTURE" ] && T=$(date +%s) && LIBGL_ALWAYS_SOFTWARE=1 nice -n 15 timeout 1200 xvfb-run -a -s "-screen 0 1920x1080x24" $U -batchmode -force-glcore -quit -projectPath $P -buildTarget Android -executeMethod SkiViewCapture.BuildAndCapture -logFile $P/Logs-capture.log; echo "capture exit=$? $(( $(date +%s)-T ))s"
waitfree
T=$(date +%s); nice -n 15 timeout 3600 $U -batchmode -nographics -quit -projectPath $P -buildTarget Android -executeMethod SkiViewBuild.Android -logFile $P/Logs-android.log; echo "android exit=$? $(( $(date +%s)-T ))s"
waitfree
T=$(date +%s); nice -n 15 timeout 5400 $U -batchmode -nographics -quit -projectPath $P -buildTarget WebGL -executeMethod SkiViewBuild.WebGL -logFile $P/Logs-webgl.log; echo "webgl exit=$? $(( $(date +%s)-T ))s"
echo ALLDONE
