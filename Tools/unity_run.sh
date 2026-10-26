#!/bin/bash
M=$1; LOG=/workspace/skiview-unity/Logs-diag.log; cd /workspace/skiview-unity
for i in $(seq 1 120); do if ! bash Tools/mamta_busy.sh >/dev/null; then sleep 10; bash Tools/mamta_busy.sh >/dev/null || break; fi; sleep 15; done
if bash Tools/mamta_busy.sh >/dev/null; then echo "still busy, not starting"; exit 3; fi
pgrep -f "Editor/Unity -batchmode" >/dev/null && { echo "another Unity is running"; exit 4; }
m=$(df -BM --output=avail / | tail -1 | tr -dc 0-9); [ "$m" -ge 15360 ] || { echo "only $m MB free"; exit 5; }
echo "start $(date '+%T %Z') $M"
LIBGL_ALWAYS_SOFTWARE=1 nice -n 15 timeout 3600 xvfb-run -a -s "-screen 0 1920x1080x24" ~/Unity/6000.3.26f1/Editor/Unity -batchmode -force-glcore -quit -projectPath /workspace/skiview-unity -executeMethod $M -logFile $LOG
echo "exit=$? $(date '+%T %Z')"; grep -E "error CS" $LOG | sort -u | head -20
cd /workspace/skiview-unity && git checkout -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta 2>/dev/null; [ -f Assets/AddressableAssetsData/link.xml ] && echo "link.xml ok" || echo "link.xml MISSING"
