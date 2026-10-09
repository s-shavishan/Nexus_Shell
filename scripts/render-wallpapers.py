"""Optional asset authoring: pip install cairosvg, then run this script.

Exports the original Nexus vector ribbons once. Building/running Nexus uses
the checked-in PNGs and needs neither Python nor CairoSVG.
"""
from pathlib import Path
import cairosvg

paths = [
    "M -200,-150 H 1800 V 1150 H -200 Z",
    "M -200,-120 H 1800 V 280 C 1300,120 980,580 610,320 C 330,100 80,500 -200,370 Z",
    "M -180,730 C 130,470 190,110 620,350 C 1000,560 1120,890 1780,330 L 1780,1120 H -180 Z",
    "M -200,890 C 410,1000 590,510 1040,600 C 1410,675 1540,530 1780,680 L 1780,1150 H -200 Z",
]
moods = {
    "Solstice": ["FFD3AD", "D68267", "9E5B71", "493251"],
    "Ember": ["CD683C", "AF3546", "682637", "271F3E"],
    "Opal": ["ECD9F8", "B0B7EA", "9BBACF", "576CAB"],
    "Lagoon": ["547C82", "315F66", "28425D", "102D37"],
    "Graphite": ["6A7690", "414C6A", "293B51", "171E36"],
    "Pearl": ["7D668D", "515A84", "37526D", "1F2B48"],
}
output = Path(__file__).resolve().parents[1] / "src/Nexus.Shell/Assets/Wallpapers"
output.mkdir(parents=True, exist_ok=True)
for name, colors in moods.items():
    pairs = [(colors[0], colors[3]), *zip(colors, colors[1:])]
    defs = "".join(
        f'<linearGradient id="g{i}" x1="0" y1="0" x2="1" y2="1">'
        f'<stop offset="0" stop-color="#{first}"/><stop offset="1" stop-color="#{last}"/></linearGradient>'
        for i, (first, last) in enumerate(pairs)
    )
    ribbons = "".join(f'<path d="{path}" fill="url(#g{i})"/>' for i, path in enumerate(paths))
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1000" viewBox="0 0 1600 1000"><defs>{defs}</defs>{ribbons}</svg>'
    png = cairosvg.svg2png(bytestring=svg.encode(), output_width=2560, output_height=1600)
    assert png.endswith(b"\x00\x00\x00\x00IEND\xaeB\x60\x82"), "Incomplete wallpaper export"
    (output / (name + ".png")).write_bytes(png)
    print(name + ".png")
