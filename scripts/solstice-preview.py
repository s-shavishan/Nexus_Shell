"""Source-derived layout references, not native Windows screenshots.

Reads shipped wallpaper paths, original icons and exported runtime palette
values. Native acrylic, fonts, scrollbars and shadow rendering require Windows.
Run with Python + cairosvg; the app itself does not depend on this script.
"""
from pathlib import Path
from html import escape
import re,json,xml.etree.ElementTree as ET
import cairosvg
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs';W,H=1440,900
DATA=json.loads((OUT/'Solstice-palettes.json').read_text())
X='{http://schemas.microsoft.com/winfx/2006/xaml}'
WINDOW=ET.parse(ROOT/'src/Nexus.Shell/MainWindow.xaml').getroot()
NAMES={n.attrib[X+'Name']:n for n in WINDOW.iter() if X+'Name' in n.attrib}
CODE=(ROOT/'src/Nexus.Shell/MainWindow.Polish.cs').read_text()
parts=[];tokens={};ink=muted=accent=''
def add(s):parts.append(s)
def color(v):return '#'+v[-6:],int(v[:2],16)/255

def rect(x,y,w,h,key='NexusCard',radius=10,shadow=False):
 c,a=color(tokens.get(key,key))
 add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{c}" fill-opacity="{a}"'+(' filter="url(#shadow)"' if shadow else '')+'/>' )
 bc,ba=color(tokens['NexusBorder']);add(f'<rect x="{x+.5}" y="{y+.5}" width="{w-1}" height="{h-1}" rx="{radius}" fill="none" stroke="{bc}" stroke-opacity="{ba}"/>')
def text(x,y,s,size=13,c=None,weight=400,anchor='start'):
 add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{c or ink}" font-weight="{weight}" text-anchor="{anchor}">{escape(s)}</text>')
def icon(name,x,y,size=52):
 source=(ROOT/'src/Nexus.Shell/Assets/Icons'/f'{name}.svg').read_text()
 contents=source[source.index('>')+1:source.rfind('</svg>')];prefix=f'i{len(parts)}-'
 for key in re.findall('id="([^"]+)"',contents):
  contents=contents.replace(f'id="{key}"',f'id="{prefix}{key}"').replace(f'url(#{key})',f'url(#{prefix}{key})')
 add(f'<svg x="{x}" y="{y}" width="{size}" height="{size}" viewBox="0 0 80 80">{contents}</svg>')
def button(x,y,w,label,primary=False):
 rect(x,y,w,34,'NexusAccent' if primary else 'NexusCard',10)
 text(x+w/2,y+22,label,13,color(tokens['NexusAccentText'])[0] if primary else ink,600 if primary else 400,'middle')
def field(x,y,w,label):
 rect(x,y,w,34,'NexusInput',10);text(x+12,y+22,label,13,muted)
def wallpaper(mood):
 raw=re.search('"'+mood+r'" => \[([^\]]+)\]',CODE)
 colors=re.findall('"([A-F0-9]{8})"',raw[1])
 add('<g transform="scale(.9)">')
 for name,first,last in [('WallpaperBase',0,3),('WallpaperRibbon',0,1),('WallpaperFold',1,2),('WallpaperHorizon',2,3)]:
  add(f'<defs><linearGradient id="{name}" x1="0%" y1="0%" x2="100%" y2="100%"><stop stop-color="{color(colors[first])[0]}"/><stop offset="1" stop-color="{color(colors[last])[0]}"/></linearGradient></defs>')
  add(f'<path d="{NAMES[name].attrib["Data"]}" fill="url(#{name})"/>')
 c,a=color(tokens['NexusHighlight']);add(f'<path d="{NAMES["WallpaperEdge"].attrib["Data"]}" fill="none" stroke="{c}" stroke-opacity="{a}" stroke-width="1.4"/>');add('</g>')
def base(mood,page):
 global tokens,ink,muted,accent
 parts.clear();tokens=DATA[mood]['Tokens'];ink=color(tokens['NexusText'])[0];muted=color(tokens['NexusMuted'])[0];accent=color(tokens['NexusAccent'])[0]
 add('<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="936" viewBox="0 0 1440 936">')
 add('<defs><filter id="shadow" x="-20%" y="-25%" width="140%" height="155%"><feGaussianBlur in="SourceAlpha" stdDeviation="4" result="blur"/><feOffset in="blur" dy="5" result="offset"/><feFlood flood-color="#1b1422" flood-opacity=".14"/><feComposite in2="offset" operator="in" result="shade"/><feMerge><feMergeNode in="shade"/><feMergeNode in="SourceGraphic"/></feMerge></filter></defs><g font-family="Segoe UI, DejaVu Sans, sans-serif">')
 wallpaper(mood);rect(12,5,1416,38,'NexusShell',12)
 icon('Nexus',25,10,28);text(64,30,'NEXUS',13,ink,600)
 for x,label in [(145,'Desktop'),(225,'Workspaces'),(330,'Explore'),(411,'Study'),(479,'Apps'),(543,'More')]:
  if label==page:rect(x-9,9,len(label)*8+18,30,'NexusSelection',8)
  text(x,30,label,13)
 text(1211,30,'Thu  07:31',12);text(1323,30,'↗',18);text(1363,30,'⚙',18);text(1400,31,'×',23)
 rect(36,51,70,30,'NexusDesktopLabel',10);text(49,73,'‹    ›',20,'#f5f8ff')
 rect(116,53,208,26,'NexusDesktopLabel',10);text(127,71,page+'  /  Personal',12,'#f5f8ff')
 rect(1190,51,214,30,'NexusDesktopLabel',10);text(1202,72,'Search your orbit   Ctrl K',12,'#f5f8ff')
 names=['Nexus','Explore','Study','Browser','Terminal','Files','Settings','Search','Windows']
 dw=12*2+60*len(names)+4*(len(names)-1);dx=(W-dw)/2
 rect(dx,810,dw,80,'NexusShell',24,True)
 for i,name in enumerate(names):
  ix=dx+12+i*64;icon(name,ix+4,824,52)
  if (page=='Desktop' and i==0) or (page=='Explore' and i==1):
   add(f'<rect x="{ix+27}" y="880" width="5" height="3" rx="1.5" fill="{accent}"/>')
 text(32,865,'Nexus · native desktop',11,'#f5f8ff');text(1404,865,'Control center',12,'#f5f8ff',400,'end')
def desktop(mood):
 base(mood,'Desktop')
 x=36
 rect(x,110,252,174,'NexusCard',20,True);text(x+20,139,'Thursday, 8 October',14,muted);text(x+17,213,'07:31',60,ink,300);text(x+20,259,'Good morning, Shan.',13)
 rect(x,296,252,210,'NexusCard',20,True);text(x+18,325,'Shan’s space',16,ink,600)
 for y,label,n in [(365,'Personal','4'),(400,'Study','6'),(435,'Build','3')]:text(x+30,y,label,13);text(x+227,y,n,11,muted,400,'end')
 text(x+30,481,'Capture a note',13,accent)
 rect(x,518,252,152,'NexusCard',20,True);text(x+18,547,'A moment of focus',13,muted);text(x+18,590,'25:00',34,ink,300)
 button(x+18,615,135,'Start focus',True);text(x+184,637,'Study',13)
 rect(x,690,152,30,'NexusDesktopLabel',10);text(x+12,710,'Open my files',13,'#f5f8ff')
 rect(x,732,176,30,'NexusDesktopLabel',10);text(x+12,752,'Windows settings',13,'#f5f8ff')
 for i,(name,label) in enumerate([('Files','My files'),('Explore','Explore'),('Study','Study'),('Apps','App Library'),('Settings','PC controls')]):
  sx=1153+i%2*118;sy=112+i//2*122
  icon(name,sx+22,sy,64);rect(sx+4,sy+75,100,24,'NexusDesktopLabel',7);text(sx+54,sy+92,label,12,'#f5f8ff',400,'middle')
 rect(320,613,340,176,'NexusCard',20,True);icon('Explore',338,631,38);text(386,656,'Personal  ⌄',16,ink,600);text(338,691,'Your everyday essentials.',12,muted);text(338,718,'4 apps · 4 saved items',11,muted);button(338,738,146,'Open workspace',True);text(500,760,'All spaces',13)
def explore(mood):
 base(mood,'Explore');x,y,w,h=130,102,1180,692
 rect(x,y,w,h,'NexusPanel',22,True);rect(x,y,w,52,'NexusSidebar',22)
 for i,c in enumerate(['#ff5f57','#febc2e','#28c840']):add(f'<circle cx="{x+25+i*26}" cy="{y+26}" r="5.5" fill="{c}"/>')
 text(x+125,y+32,'‹   ›',20,muted);text(x+w/2,y+32,'Explore',14,ink,600,'middle');text(x+w-106,y+32,'⌕',20,muted);text(x+w-70,y+32,'+',20,muted);text(x+w-35,y+32,'≡',20,muted)
 rect(x,y+52,176,h-82,'NexusSidebar',0);text(x+22,y+84,'WORKSPACE',10,muted)
 for i,(name,label) in enumerate([('Explore','Workspaces'),('Nexus','Desktop'),('Browser','Explore'),('Study','Study'),('Apps','Apps'),('Game','Games'),('Document','Activity')]):
  sy=y+106+i*38
  if label=='Explore':rect(x+10,sy-5,156,34,'NexusSelection',8)
  icon(name,x+20,sy,21);text(x+53,sy+16,label,13)
 text(x+22,y+404,'YOUR SESSION',10,muted)
 for i,(name,label) in enumerate([('Windows','Window overview'),('Settings','PC controls'),('Note','Personalize')]):
  sy=y+426+i*38;icon(name,x+20,sy,21);text(x+53,sy+16,label,12)
 text(x+23,y+h-62,'Shan',13,ink,600);text(x+23,y+h-43,'Personal workspace',11,muted)
 cx=x+198;text(cx,y+87,'Study',24,ink,600);text(cx,y+111,'Resources for the things you are learning.',12,muted)
 rect(cx,y+127,310,36,'NexusSelection',10)
 for i,label in enumerate(['Personal  4','Study  6','Build  3']):
  if i==1:rect(cx+105,y+131,98,28,'NexusSegment',7)
  text(cx+16+i*102,y+151,label,12)
 rect(cx,y+175,960,44,'NexusInput',10);text(cx+14,y+203,'Paste a link or jot down a note…',13,muted);button(cx+852,y+180,68,'Save',True);text(cx+936,y+203,'+',20,accent)
 field(cx,y+232,580,'Search this space');field(cx+592,y+232,160,'All collections');text(cx+780,y+254,'☆',20,muted);rect(cx+820,y+235,42,28,'NexusSegment',7);text(cx+834,y+255,'▦',18,accent);text(cx+889,y+255,'☷',18,muted)
 # GridView still uses the existing 230×212 virtualized items. Three columns
 # fit the finite viewport here; sample data does not alter shipped settings.
 cards=[('Browser','Microsoft Learn','Link','An introduction to C# and .NET.','Resources'),('Note','Ideas for NEXUS','Note','Make the desktop feel calm.','Ideas'),('Files','A/L resources','Folder','My references for this week.','Study')]
 for i,(name,title,kind,note,coll) in enumerate(cards):
  bx=cx+i*244;by=y+285;rect(bx,by,234,212,'NexusCard',16);icon(name,bx+16,by+16,42);text(bx+218,by+38,kind,10,muted,400,'end');text(bx+16,by+87,title,15,ink,600);text(bx+16,by+113,note,12,muted);text(bx+16,by+189,coll,10,accent)
 text(cx,y+617,'3 shown · 3 in this space',11,muted)
 rect(x,y+h-30,w,30,'NexusSidebar',0);text(cx,y+h-10,'Study · 3 / 100 items · Stored on this PC',11,muted);text(x+w-16,y+h-10,'NEXUS  1.1.0',9,muted,400,'end')
def finish(mood,page):
 add('<rect x="0" y="900" width="1440" height="36" fill="#f5f3f0"/>');text(24,923,'NEXUS 1.1.0 · '+mood+' · '+page+' · layout reference with sample content',12,'#625b59');text(1416,923,'Native Windows rendering pending',11,'#625b59',400,'end');add('</g></svg>')
 stem='Nexus-1.1.0-'+mood+'-'+page.lower()+'-reference';svg=OUT/(stem+'.svg');svg.write_text(''.join(parts));cairosvg.svg2png(url=str(svg),write_to=str(OUT/(stem+'.png')))
if __name__=='__main__':
 for mood in ['Solstice','Ember']:
  desktop(mood);finish(mood,'Desktop')
  explore(mood);finish(mood,'Explore')
 print('Four source-derived layout references exported')
