"""Draw a documentation design reference. Not a Windows/WinUI screenshot.
Optional rendering needs CairoSVG and Pillow; neither is an app dependency.
"""
from pathlib import Path
from html import escape
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
out = root / "docs"
svg = []
def add(value): svg.append(value)
def rect(x,y,w,h,fill,rx=0,stroke=None,opacity=1):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" fill-opacity="{opacity}"'+(f' stroke="{stroke}" stroke-opacity=".25"' if stroke else '')+'/>')
def text(x,y,value,size=13,fill="#F2F3FD",weight=400,anchor="start",spacing=None):
    add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{fill}" font-weight="{weight}" text-anchor="{anchor}"'+(f' letter-spacing="{spacing}"' if spacing else '')+f'>{escape(value)}</text>')
def icon(kind,x,y,color="#EFF1FD",size=22):
    paths = {
        "files": '<path d="M2 7V4h7l3 3h10v14H2Z"/>',
        "browser": '<circle cx="12" cy="12" r="10"/><ellipse cx="12" cy="12" rx="4.5" ry="10"/><path d="M2 12h20"/>',
        "code": '<path d="m8 5-6 7 6 7m8-14 6 7-6 7m-3-15-3 18"/>',
        "terminal": '<rect x="1.5" y="3" width="21" height="18" rx="3"/><path d="m5 8 4 4-4 4m7 0h6"/>',
        "settings": '<circle cx="12" cy="12" r="4"/><path d="m12 1 2 4 4-1 2 3-2 4 3 2-1 4-4 1-2 4-4-1-1-4-4-2 1-4-3-2 2-4 4 1 3-3Z"/>',
        "home": '<path d="m2 10 10-8 10 8M5 9v13h14V9m-10 13v-8h6v8"/>',
        "apps": '<rect x="2" y="2" width="8" height="8" rx="2"/><rect x="14" y="2" width="8" height="8" rx="2"/><rect x="2" y="14" width="8" height="8" rx="2"/><rect x="14" y="14" width="8" height="8" rx="2"/>',
        "game": '<path d="M6 7h12c5 0 8 14 4 15-2 1-4-4-6-4H8c-2 0-4 5-6 4C-2 21 1 7 6 7Z"/><path d="M7 10v7m-3-3.5h6m6-2h.2m3 3h.2"/>',
        "activity": '<path d="M2 21h20M5 17v-6m7 6V3m7 14V7"/>',
        "running": '<rect x="1" y="2" width="15" height="14" rx="2"/><rect x="7" y="8" width="16" height="14" rx="2"/>',
        "focus": '<path d="M19 18a9 9 0 0 1-13-13 9 9 0 1 0 13 13Z"/>',
        "search": '<circle cx="10" cy="10" r="7"/><path d="m15 15 7 7"/>',
        "exit": '<path d="m5 5 14 14M5 19 19 5"/>',
        "expand": '<path d="M2 9V2h7m6 0h7v7M2 15v7h7m6 0h7v-7"/>',
        "menu": '<path d="M3 6h18M3 12h18M3 18h18"/>',
    }
    add(f'<g transform="translate({x},{y}) scale({size/24})" fill="none" stroke="{color}" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">{paths[kind]}</g>')
add('<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="940" viewBox="0 0 1440 940" role="img">')
add('<title>Nexus Orbit design reference — not a Windows runtime screenshot</title><desc>Code-drawn reference using the native source palette and sample application pins.</desc>')
add('<defs><linearGradient id="wallpaper" x2="1" y2="1"><stop stop-color="#18162B"/><stop offset=".52" stop-color="#262C47"/><stop offset="1" stop-color="#0F2A2F"/></linearGradient><linearGradient id="hero" x2="1" y2="1"><stop stop-color="#494361"/><stop offset=".7" stop-color="#2A354E"/><stop offset="1" stop-color="#29474F"/></linearGradient></defs>')
add('<g font-family="DejaVu Sans, sans-serif">')
rect(0,0,1440,940,"url(#wallpaper)")
# Draw the actual static wallpaper paths from XAML, with its Viewbox scaling.
ns={"p":"http://schemas.microsoft.com/winfx/2006/xaml/presentation"}
xml=ET.parse(root/"src/Nexus.Shell/MainWindow.xaml").getroot()
canvas=xml.find('.//p:Viewbox/p:Canvas',ns)
add('<g transform="translate(-32,0) scale(.94)" opacity=".85">')
for i,node in enumerate(canvas):
    data=node.attrib.get("Data")
    if not data:continue
    fill=node.find("p:Path.Fill/p:LinearGradientBrush",ns)
    if fill is not None:
        add(f'<defs><linearGradient id="ribbon{i}" x2="1" y2="1">')
        for stop in fill:
            add(f'<stop offset="{stop.attrib["Offset"]}" stop-color="{stop.attrib["Color"]}"/>')
        add('</linearGradient></defs>')
        add(f'<path d="{data}" fill="url(#ribbon{i})"/>')
    else:
        color=node.attrib["Stroke"]
        alpha=int(color[1:3],16)/255 if len(color)==9 else 1
        rgb="#"+color[-6:]
        add(f'<path d="{data}" fill="none" stroke="{rgb}" stroke-opacity="{alpha}" stroke-width="{node.attrib["StrokeThickness"]}"/>')
add('</g>')
rect(0,0,1440,44,"#0C111D",opacity=.75)
rect(20,9,26,26,"#D0C7FF",7);text(33,29,"N",19,"#24203B",600,"middle")
text(55,28,"Nexus",14,weight=600)
for x,label in [(151,"Home"),(219,"Explore"),(303,"Study"),(374,"Apps"),(434,"Games"),(502,"Activity")]:text(x,28,label,13)
text(1210,28,"Wed  08:35",12,anchor="end")
icon("expand",1249,15,size=16);icon("settings",1294,15,size=16);icon("exit",1341,15,size=16)
text(32,81,"WHITE DREAMS  /  NEXUS ORBIT",10,"#A9B0C9",spacing=1.2)
icon("search",1174,66,"#A9B0C9",14);text(1198,80,"Search your orbit",12);text(1340,80,"Ctrl K",11,"#A9B0C9")
rect(32,115,107,26,"#3E4565",10,opacity=.35);text(44,133,"Your orbit",11,"#C7BDFF")
text(32,163,"Wednesday, 7 October",14,"#A9B0C9");text(26,251,"08:35",78,weight=300)
text(32,290,"Good morning, Shan.",16)
rect(32,335,232,154,"#353C5C",20,"#D9DEFF",.5)
icon("focus",51,356,"#C7BDFF",22);text(51,410,"Shan’s space",18)
text(51,439,"A little space for your",13,"#A9B0C9");text(51,460,"next big idea.",13,"#A9B0C9")
text(43,530,"Open my files",13);text(43,570,"Windows settings",13)
# Floating workspace, sidebar and chrome.
rect(296,106,1112,730,"#192033",23,"#D9DEFF",.91)
rect(297,107,1110,49,"#3C435F",22,opacity=.44)
rect(297,134,1110,22,"#3C435F",opacity=.44)
for x,c in [(322,"#FF7F88"),(348,"#F1CE80"),(374,"#87DCC2")]:add(f'<circle cx="{x}" cy="131" r="5.5" fill="{c}"/>')
text(852,135,"Window overview",13,"#A9B0C9",anchor="middle");icon("menu",1367,121,"#CBD0E8",17)
rect(297,157,183,644,"#0D1321",opacity=.5)
add('<path d="M480 156v646" stroke="#D9DEFF" stroke-opacity=".1"/>')
text(322,192,"WORKSPACE",10,"#A9B0C9",spacing=1.0)
for i,(kind,label) in enumerate([("home","Home"),("browser","Explore"),("focus","Study"),("apps","Apps"),("game","Games"),("activity","Activity")]):
    y=210+i*44

    icon(kind,322,y+12,"#DEDFF1",17);text(351,y+27,label,13)
text(322,505,"YOUR SESSION",10,"#A9B0C9",spacing=1.0)
rect(310,523,156,42,"#BEABF8",10,opacity=.16)
icon("running",322,535,size=17);text(351,550,"Window overview",12)
icon("settings",322,578,size=17);text(351,593,"Settings",13)
add('<path d="M319 730h142" stroke="#D9DEFF" stroke-opacity=".2"/>')
text(319,759,"Shan",13);text(319,780,"Personal workspace",11,"#A9B0C9")
# Sample window titles; this is a design reference, not captured user data.
text(504,210,"Everything in your orbit.",28,weight=600)
text(504,239,"Find an open window and return to it. Ctrl+4 opens this view.",12,"#A9B0C9")
rect(504,256,826,36,"#141B30",10,"#D9DEFF",.9)
icon("search",518,267,"#A9B0C9",15);text(545,280,"Find a window or app",13,"#A9B0C9")
rect(1340,256,44,36,"#252D45",10,"#D9DEFF",.8)
text(1362,281,"↻",22,"#D2C7FF",anchor="middle")
text(504,319,"6 of 6 open windows",11,"#A9B0C9")
windows=[("Code",["Nexus Shell —", "MainWindow.xaml"]),
         ("firefox",["A little inspiration", "— Firefox"]),
         ("explorer",["Pictures"]),
         ("WindowsTerminal",["PowerShell"]),
         ("Notepad",["Today’s ideas"]),
         ("ApplicationFrameHost",["Personalization"]) ]
for i,(process,title) in enumerate(windows):
    x=504+(i%3)*292; y=337+(i//3)*192
    rect(x,y,280,180,"#252D45",18,"#D9DEFF",.94)
    rect(x+17,y+17,36,36,"#151D30",11)
    icon("running",x+26,y+26,"#C7BDFF",18)
    text(x+64,y+40,process,11,"#A9B0C9")
    for j,line in enumerate(title):text(x+17,y+89+j*23,line,16)
    text(x+17,y+158,"Return to this window ↗",10,"#C7BDFF")
# Space below the bounded grid stays calm; it is not filled with live thumbnails.

rect(297,802,1110,33,"#0E1525",22,opacity=.63);rect(297,802,1110,15,"#0E1525",opacity=.63)
text(314,823,"6 visible windows · Refreshes while this view is active",11,"#A9B0C9");text(1389,823,"NEXUS  0.4",9,"#A9B0C9",anchor="end",spacing=.8)
text(32,893,"Nexus · native desktop",11,"#A9B0C9");text(1268,893,"Control center",12)
# Dock: stationary buttons contain moving icon chrome in the native app.
pins=[("files","Files","#3B7085"),("browser","Firefox","#3B5691"),("code","VS Code","#625091"),("browser","Browser","#3B5691"),("terminal","Terminal","#625091")]
rect(455,851,530,76,"#4C526D",23,"#DFDFFF",.69)
rect(469,865,48,48,"#C4B5EB",13);text(493,899,"N",27,"#24243C",anchor="middle")
add('<path d="M533 873v32" stroke="#D9DEFF" stroke-opacity=".3"/>')
for i,(kind,label,color) in enumerate(pins[:5]):
    x=549+i*61
    rect(x,865,48,48,color,13,"#FFFFFF");icon(kind,x+13,878,size=22)
rect(856,865,48,48,"#545078",13,"#FFFFFF");icon("search",869,878,size=22)
rect(917,865,48,48,"#454C67",13,"#FFFFFF");icon("running",930,878,size=22)
add('</g></svg>')
out.mkdir(exist_ok=True)
(out/"Nexus-Orbit-preview.svg").write_text("\n".join(svg),encoding="utf-8")
print(out/"Nexus-Orbit-preview.svg")
if __name__ == "__main__":
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument("--png",action="store_true")
    args=parser.parse_args()
    if args.png:
        import cairosvg
        cairosvg.svg2png(url=str(out/"Nexus-Orbit-preview.svg"),write_to=str(out/"Nexus-Orbit-preview.png"))
        print(out/"Nexus-Orbit-preview.png")
