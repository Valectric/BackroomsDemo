#!/bin/bash
#
# Regenerates every itch.io page image, reproducibly: cover, banner and background tile.
#
# The picture is not artwork — it is a real frame from the game, so it cannot misrepresent what a
# player gets. Everything that decides what it looks like is recorded here:
#
#   Source      Screenshots/eye-1-THE-YELLOW-ROOMS.png
#   Produced by FloorLookTests, eye-level capture of floor 1
#   Seed        977  (FloorLookTests builds floor N with seed N * 977)
#   Framing     the lone chair centre, the green stairwell sign in the fog to the left
#
# To recreate the source frame from scratch:
#   mooserunnerCli test --class Backrooms.MazeManager.Tests FloorLookTests
#
# Then run this. Output is 630x500, itch.io's recommended cover size.
#
set -euo pipefail

cd "$(dirname "$0")/.."

SOURCE="Screenshots/eye-1-THE-YELLOW-ROOMS.png"
OUT="${1:-cover.png}"
TITLE_TOP="${2:-BACKROOMS}"
TITLE_BOTTOM="${3:-RELICS}"

if [ ! -f "$SOURCE" ]; then
  echo "$SOURCE is missing — run FloorLookTests first to regenerate it." >&2
  exit 1
fi

# Bahnschrift: wide, slightly technical letterforms that read as signage, which suits a building
# that should not exist. The page font itself cannot be changed — itch only allows that through
# custom CSS, which is granted per account on request — so the only typography under our control is
# what is baked into these images. That makes the choice matter more than it normally would.
FONT_BOLD='C\:/Windows/Fonts/bahnschrift.ttf'
FONT_PLAIN='C\:/Windows/Fonts/bahnschrift.ttf'

# A stack of increasingly opaque bands, standing in for a gradient. A single dark box left a hard
# horizontal seam across the middle of the picture, which looked like a mistake rather than a design.
BANDS=""
for i in $(seq 0 15); do
  y=$((330 + i * 11))
  a=$(awk "BEGIN{printf \"%.3f\", 0.06 + $i * 0.035}")
  BANDS="${BANDS}drawbox=y=${y}:w=iw:h=12:color=black@${a}:t=fill,"
done

ffmpeg -y -loglevel error -i "$SOURCE" -vf "\
crop=907:700:186:0,\
scale=630:500,\
eq=contrast=1.08:saturation=1.14,\
${BANDS}\
drawtext=fontfile='${FONT_BOLD}':text='${TITLE_TOP}':fontcolor=0xF7F4E8:fontsize=52:x=(w-tw)/2:y=h-132:shadowcolor=black@0.85:shadowx=2:shadowy=2,\
drawtext=fontfile='${FONT_BOLD}':text='${TITLE_BOTTOM}':fontcolor=0xE8C34A:fontsize=52:x=(w-tw)/2:y=h-80:shadowcolor=black@0.85:shadowx=2:shadowy=2,\
drawtext=fontfile='${FONT_PLAIN}':text='Find the stairs. Something else is already here.':fontcolor=0xC6C0AE:fontsize=16:x=(w-tw)/2:y=h-26" \
  "$OUT"

echo "wrote $OUT ($(ffprobe -v error -select_streams v -show_entries stream=width,height -of csv=p=0 "$OUT"))"

# ---------------------------------------------------------------------------------------------
# Banner — 960x400, shown across the top of the page (Edit theme > Banner).
# ---------------------------------------------------------------------------------------------
BANNER="Screenshots/press/banner.png"
ffmpeg -y -loglevel error -i "Screenshots/press/01-yellow-rooms.png" -vf "crop=1280:534:0:100,scale=960:400,drawbox=y=250:w=iw:h=150:color=black@0.45:t=fill,drawtext=fontfile='${FONT_BOLD}':text='${TITLE_TOP} ${TITLE_BOTTOM}':fontcolor=0xF7F4E8:fontsize=58:x=(w-tw)/2:y=h-116:shadowcolor=black@0.85:shadowx=2:shadowy=2,drawtext=fontfile='${FONT_PLAIN}':text='999 FLOORS  ·  SOMETHING IS ALREADY HERE':fontcolor=0xE8C34A:fontsize=20:x=(w-tw)/2:y=h-46"   "$BANNER"
echo "wrote $BANNER"

# ---------------------------------------------------------------------------------------------
# Background tile — the wallpaper texture itself, exported by PageBackground:
#
#   mooserunnerCli test --class Backrooms.MazeManager.Tests PageBackground
#
# Not derived from a screenshot. A photographed wall carries the lighting of the room it was taken
# in, so tiling it repeats that light and dark as visible banding across the page. The texture has
# no lighting in it at all, which is exactly why the game can tile it across every wall on a floor
# without a seam.
# ---------------------------------------------------------------------------------------------
echo "background tile: run the PageBackground test to export Screenshots/press/page-tile.png"
