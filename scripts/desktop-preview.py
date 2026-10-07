"""Generate source-grounded 0.9.0 layout references, not native screenshots.

Uses the actual wallpaper paths and shipped vector assets. Sample user content.
Export SVGs to PNG with Inkscape or another SVG renderer.
"""
from pathlib import Path
from html import escape
import xml.etree.ElementTree as ET
import re

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs'
INK,MUTED,ACCENT='#1b273d','#4e5a70','#3163bc'
parts=[]
def add(s):parts.append(s)
def box(x,y,w,h,fill='#f8f8fc',r=12,opacity=.92,shadow=False):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" fill-opacity="{opacity}" stroke="#ffffff" stroke-opacity=".55"'+(' filter="url(#shadow)"' if shadow else '')+'/>')
def text(x,y,s,size=13,color=INK,weight=400,anchor='start'):
    add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" font-weight="{weight}" text-anchor="{anchor}">{escape(s)}</text>')
def icon(name,x,y,size=48):
    svg=(ROOT/'src/Nexus.Shell/Assets/Icons'/f'{name}.svg').read_text()
    content=svg[svg.index('>')+1:svg.rfind('</svg>')]
    prefix=f'icon{len(parts)}-'
    for identifier in re.findall(r'id="([^"]+)"',content):
        content=content.replace(f'id="{identifier}"',f'id="{prefix}{identifier}"').replace(f'url(#{identifier})',f'url(#{prefix}{identifier})')
    add(f'<svg x="{x}" y="{y}" width="{size}" height="{size}" viewBox="0 0 80 80">{content}</svg>')
def button(x,y,w,label,primary=False):
    box(x,y,w,32,ACCENT if primary else '#e7eaf4',8,.95)
    text(x+w/2,y+21,label,12,'white' if primary else INK,500,'middle')
def start(page):
    parts.clear()
    add('<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1000" viewBox="0 0 1600 1000">')
    add('<defs><linearGradient id="sky" x2="1" y2="1"><stop stop-color="#293668"/><stop offset="1" stop-color="#3f2b67"/></linearGradient><filter id="shadow" x="-30%" y="-30%" width="160%" height="170%"><feGaussianBlur in="SourceAlpha" stdDeviation="10" result="blur"/><feOffset in="blur" dy="10" result="offset"/><feFlood flood-color="#38486f" flood-opacity=".18"/><feComposite in2="offset" operator="in" result="shade"/><feMerge><feMergeNode in="shade"/><feMergeNode in="SourceGraphic"/></feMerge></filter></defs><g font-family="DejaVu Sans, Segoe UI, sans-serif">')
    box(0,0,1600,1000,'url(#sky)',0,1)
    xns='{http://schemas.microsoft.com/winfx/2006/xaml}'
    tree=ET.parse(ROOT/'src/Nexus.Shell/MainWindow.xaml')
    wallpaper=next(n for n in tree.getroot().iter() if n.attrib.get(xns+'Name')=='WallpaperAccents')
    add('<g opacity=".94">')
    for i,node in enumerate(n for n in wallpaper.iter() if n.tag.rsplit('}',1)[-1]=='Path'):
        stops=[n.attrib for n in node.iter() if n.tag.rsplit('}',1)[-1]=='GradientStop']
        if stops:
            add(f'<defs><linearGradient id="wall{i}" x2="1" y2="1">'+''.join(f'<stop offset="{n["Offset"]}" stop-color="{n["Color"]}"/>' for n in stops)+'</linearGradient></defs>')
            add(f'<path d="{node.attrib["Data"]}" fill="url(#wall{i})"/>')
        else:add(f'<path d="{node.attrib["Data"]}" fill="none" stroke="#fff0ee" stroke-opacity=".33" stroke-width="2"/>')
    add('</g>')
    box(8,5,1584,38,'#f0f0f8',10,.82)
    icon('Nexus',20,10,27);text(57,29,'NEXUS',12,INK,600)
    for x,label in [(156,'Desktop'),(247,'Workspaces'),(357,'Explore'),(440,'Study'),(507,'Apps')]:text(x,29,label,12,ACCENT if label==page else MUTED,500)
    text(1431,29,'Wed  22:29',12);text(1552,29,'•••',15)
    box(20,56,150,28,'#272d50',8,.46);text(35,75,'‹   ›',18,'white');text(72,75,page,11,'white')
    box(1334,56,248,28,'#272d50',8,.46);text(1458,75,'⌕   Search your orbit    Ctrl K',11,'white',400,'middle')
    box(500,916,600,72,'#f0f0f8',22,.82,True)
    for i,name in enumerate(['Nexus','Explore','Study','Files','Browser','Terminal','Settings','Search','Windows']):icon(name,510+i*65,925,54)
def desktop():
    box(36,120,236,164,r=20,shadow=True);text(52,151,'Wednesday, 7 October',14,MUTED)
    text(49,218,'22:29',58,INK,300);text(52,252,'Good evening, Shan.',13)
    box(36,300,236,206,r=20,shadow=True);text(52,332,'Shan’s space',17,INK,500)
    for y,name,n in [(368,'Personal','4'),(403,'Study','6'),(438,'Build','3')]:text(63,y,name,13);text(244,y,n,11,MUTED,400,'end')
    text(63,480,'Capture a note',12,ACCENT)
    box(36,522,236,148,r=20,shadow=True);text(52,554,'A moment of focus',13,MUTED);text(52,602,'25:00',34,INK,300)
    button(52,620,120,'Start focus',True);button(180,620,76,'Study')
    box(36,690,152,30,'#272d50',8,.46);text(49,710,'Open my files ↗',12,'white')
    box(36,732,172,30,'#272d50',8,.46);text(49,752,'Windows settings ↗',12,'white')
    for i,(label,name) in enumerate([('My files','Files'),('Explore','Explore'),('Study','Study'),('App Library','Apps'),('A/L resources','Document'),('Ideas for NEXUS','Note')]):
        x=1315+i%2*118;y=112+i//2*125
        icon(name,x+22,y,64);box(x+2,y+71,104,26,'#141937',6,.63);text(x+54,y+88,label,11,'#f5f8ff',400,'middle')
    box(316,714,340,172,r=20,shadow=True);icon('Explore',334,728,40);text(385,755,'Personal  ⌄',16,INK,600)
    text(334,791,'Your everyday essentials.',12,MUTED);text(334,817,'4 apps · 4 saved items',11,MUTED)
    button(334,835,144,'Open workspace',True);button(486,835,99,'All spaces')
def explore():
    box(210,118,1180,768,'#f0f1f8',20,.90,True)
    box(210,118,1180,44,'#e8eaf3',20,.85);box(210,140,1180,22,'#e8eaf3',0,.85)
    for x,c in [(233,'#ff5f57'),(257,'#febc2e'),(281,'#28c840')]:add(f'<circle cx="{x}" cy="140" r="5.5" fill="{c}"/>')
    text(800,145,'Explore',13,MUTED,500,'middle');text(1354,145,'☰',15,MUTED)
    box(210,162,170,696,'#e8eaf3',0,.85);text(234,201,'WORKSPACE',9,MUTED,500)
    for i,(label,symbol) in enumerate([('Workspaces','▤'),('Desktop','⌂'),('Explore','◈'),('Study','▱'),('Apps','▦'),('Games','◇'),('Activity','↗')]):
        y=219+i*39
        if label=='Explore':box(222,y-1,146,32,'#c9d9ef',8,.72)
        text(235,y+19,symbol,15,ACCENT if label=='Explore' else MUTED);text(263,y+19,label,12,ACCENT if label=='Explore' else INK,500)
    text(234,558,'YOUR SESSION',9,MUTED,500);text(235,598,'▤   Windows',12);text(235,639,'⚙   Personalize',12)
    text(238,824,'Shan',13,INK,600);text(238,845,'Personal workspace',10,MUTED)
    x=400;text(x,202,'Study',22,INK,600);text(x,225,'Resources for the things you are learning.',12,MUTED)
    box(x,244,294,37,'#dce2ee',10,.7)
    for i,(label,w) in enumerate([('Personal',100),('Study',83),('Build',83)]):
        bx=x+4+[0,102,187][i]
        if i==1:box(bx,247,w,31,r=8,opacity=.98)
        text(bx+w/2,267,label,12,INK,500,'middle')
    box(x,299,970,47,r=10,opacity=.92);text(x+15,329,'Paste a link or jot down a note…',13,MUTED);button(1254,307,66,'Save',True);text(1349,329,'+',20,MUTED)
    box(x,363,627,34,r=8,opacity=.92);text(x+14,386,'⌕  Search this space',12,MUTED);button(1039,364,150,'All collections');text(1220,386,'☆    ▦    ☷',16,ACCENT)
    cards=[('Browser','Microsoft Learn','Link','An introduction to C# and .NET.','Resources'),('Note','Ideas for NEXUS','Note','Make the desktop feel calm.','Ideas'),('Files','A/L resources','Folder','My references for this week.','Notes'),('Note','One thing for today','Note','Finish one ICT lesson.','Focus'),('Document','Lesson notes','File','Summary and examples.','Study'),('Browser','MDN Web Docs','Link','Learn one page at a time.','Resources')]
    for i,(name,title,kind,note,collection) in enumerate(cards):
        cx=x+i%3*227;cy=417+i//3*211
        box(cx,cy,215,197,r=16,opacity=.92);icon(name,cx+12,cy+11,42);text(cx+200,cy+35,kind,10,MUTED,400,'end')
        text(cx+16,cy+84,title,14,INK,600);text(cx+16,cy+113,note,11,MUTED);text(cx+16,cy+174,collection,10,ACCENT)
    box(1104,417,266,407,'#e8eaf3',18,.86);text(1122,448,'Ideas for NEXUS',18,INK,600);text(1350,448,'×',16,MUTED)
    text(1122,475,'Ideas · Note',11,MUTED);text(1122,499,'#design   #nexus',11,ACCENT)
    box(1122,516,228,108,r=12);text(1135,546,'Make the desktop feel calm.',12);text(1135,571,'Keep every action within reach.',12)
    button(1122,642,228,'Edit details…');button(1122,688,228,'Add to favorites');button(1122,734,228,'Move to space…');text(1122,800,'Remove from Explore…',11,MUTED)
    text(x,841,'6 shown · 6 in this space',10,MUTED);text(234,877,'Study · 13 / 100 items · Stored on this PC',10,MUTED);text(1366,877,'NEXUS 0.9.0',10,MUTED,400,'end')
def finish(name):
    box(0,986,1600,14,'#293668',0,1);text(1580,998,'0.9.0 LAYOUT REFERENCE · Sample content · Native Windows rendering unverified',10,'#f5f8ff',400,'end')
    add('</g></svg>');(OUT/(name+'.svg')).write_text('\n'.join(parts),encoding='utf-8')

if __name__=='__main__':
    start('Desktop');desktop();finish('Nexus-0.9.0-Desktop-reference')
    start('Explore');explore();finish('Nexus-0.9.0-Explore-reference')
    print('Generated source-grounded desktop and Explore references.')
