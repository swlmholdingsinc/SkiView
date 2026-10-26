#!/bin/bash
set -euo pipefail
SRC=${SV_TFRAMES_DIR:-/tmp/t19/frames}
OUT=/workspace/skiview-unity/Captures/trailer_v1
mkdir -p "$OUT"
enc() {
  local clip=$1 name=$2
  local dir="$SRC/$clip"
  local n=$(ls "$dir"/f*.jpg 2>/dev/null | wc -l)
  if [ "$n" -lt 10 ]; then echo "SKIP $clip ($n frames)"; return 1; fi
  ffmpeg -y -hide_banner -loglevel error -framerate 30 -i "$dir/f%04d.jpg" \
    -c:v libx264 -pix_fmt yuv420p -crf 18 -movflags +faststart \
    "$OUT/$name"
  ls -lh "$OUT/$name"
}
enc slalom_close tv1_slalom_close_skiview_v01.mp4
enc slalom_1st   tv1_slalom_1st_skiview_v01.mp4
enc slalom_high  tv1_slalom_high_skiview_v01.mp4
enc slalom_boat  tv1_slalom_boat_skiview_v01.mp4
enc jump_any     tv1_jump_any_skiview_v01.mp4 || true
enc beauty_any   tv1_beauty_skiview_v01.mp4 || true
# optional 4-cam grid if all slalom present
if [ -d "$SRC/slalom_close" ] && [ -d "$SRC/slalom_1st" ] && [ -d "$SRC/slalom_high" ] && [ -d "$SRC/slalom_boat" ]; then
  ffmpeg -y -hide_banner -loglevel error \
    -framerate 30 -i "$SRC/slalom_close/f%04d.jpg" \
    -framerate 30 -i "$SRC/slalom_1st/f%04d.jpg" \
    -framerate 30 -i "$SRC/slalom_high/f%04d.jpg" \
    -framerate 30 -i "$SRC/slalom_boat/f%04d.jpg" \
    -filter_complex "nullsrc=size=1920x1080:rate=30[base];[0:v]setpts=PTS-STARTPTS,scale=960:540[a];[1:v]setpts=PTS-STARTPTS,scale=960:540[b];[2:v]setpts=PTS-STARTPTS,scale=960:540[c];[3:v]setpts=PTS-STARTPTS,scale=960:540[d];[base][a]overlay=0:0[tmp1];[tmp1][b]overlay=960:0[tmp2];[tmp2][c]overlay=0:540[tmp3];[tmp3][d]overlay=960:540" \
    -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart -shortest \
    "$OUT/tv1_slalom_4cam_skiview_v01.mp4" && ls -lh "$OUT/tv1_slalom_4cam_skiview_v01.mp4" || echo "grid encode failed (nonfatal)"
fi
echo ENCODE_DONE
ls -lh "$OUT"
