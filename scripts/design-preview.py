"""Nexus Shell 0.7.0 design references. Illustrative data, not Windows screenshots.
Standard-library SVG; rasterize with a suitable SVG renderer for PNG delivery.
"""
from pathlib import Path
from html import escape
root=Path(__file__).resolve().parents[1]
out=root/'docs'
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

TEXT='#F4F7FE'; MUTED='#B8C3D6'; ACCENT='#C7BEF7'; TEAL='#9BDED8'; CARD='#2A3343'; EDGE='#6D809A'
def label(x,y,value,size=13,fill=TEXT,weight=400,anchor='start',spacing=None):text(x,y,value,size,fill,weight,anchor,spacing)
def pill(x,y,w,title,active=False):
 rect(x,y,w,38,'#344052' if active else '#273243',12,EDGE)
 label(x+w/2,y+24,title,13,ACCENT if active else TEXT,anchor='middle')
def start(title):
 svg.clear()
 add('<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="1000" viewBox="0 0 1440 1000">')
 add('<title>'+escape(title)+'</title>')
 add('<defs><linearGradient id="wallpaper" x2="1" y2="1"><stop stop-color="#121722"/><stop offset="1" stop-color="#1B2C3A"/></linearGradient><linearGradient id="hero" x2="1" y2="1"><stop stop-color="#343B54"/><stop offset="1" stop-color="#29494C"/></linearGradient><clipPath id="viewport"><rect x="296" y="134" width="1112" height="748"/></clipPath></defs>')
 add('<g font-family="DejaVu Sans, sans-serif">')
 rect(0,0,1440,1000,'url(#wallpaper)')
 add('<path d="M430,-160 C1240,-170 1320,640 1870,310 L1870,1110 C970,1200 1360,240 430,-160Z" fill="#5360A2" opacity=".09"/>')
 add('<path d="M640,-120 C1400,120 1050,750 1750,620" fill="none" stroke="#978ED1" stroke-opacity=".12"/>')
 add('<path d="M-190,780 C510,430 400,1190 1200,1110" fill="none" stroke="#5DDDCE" stroke-opacity=".05" stroke-width="160"/>')
 rect(24,16,1392,56,'#1B2433',20,EDGE)
 rect(40,29,30,30,ACCENT,10);label(55,51,'N',20,'#171C29',600,'middle');label(80,50,'NEXUS',13,weight=600,spacing=1.2)
 for x,w,title in [(168,90,'Desktop'),(262,126,'Workspaces'),(392,88,'Explore'),(484,78,'Study'),(566,70,'Apps'),(640,75,'More')]:
  label(x+w/2,50,title,13,TEXT,anchor='middle')
 label(1130,50,'Wed 14:56',12);icon('expand',1260,36,size=17);icon('settings',1308,36,size=17);icon('exit',1360,36,size=17)
 label(39,110,'←',18,MUTED);label(76,110,'→',18,MUTED);label(112,111,'NEXUS  /  PERSONALIZE  /  STUDY',10,MUTED,spacing=1.2)
 icon('search',1190,94,size=16);label(1216,108,'Search your orbit',12);label(1360,108,'Ctrl K',10,MUTED)
def frame():
 rect(32,162,126,30,'#2D3A4D',14);label(46,182,'25:00 · Focus',11,ACCENT)
 label(32,223,'NEXUS AURA / PEARL',9,MUTED,spacing=1)
 label(32,258,'Wednesday, 7 October',14,MUTED);label(27,346,'14:56',78)
 label(32,392,'Good afternoon, Shan.',16)
 rect(32,434,232,151,CARD,20,EDGE);icon('focus',52,454,ACCENT,22)
 label(52,502,'Shan’s space',18);label(52,533,'A little space for your',13,MUTED);label(52,555,'next big idea.',13,MUTED)
 label(46,625,'Open my files',13);label(46,668,'Windows settings',13)

 # Dock width reflects two selected apps, three running entries, search and overview.
 rect(445,902,550,76,'#1B2433',26,EDGE)
 rect(458,915,48,48,ACCENT,16);label(482,949,'N',27,'#171C29',anchor='middle')
 rect(517,925,1,30,EDGE)
 for x,kind,col in [(530,'browser','#344E70'),(592,'code','#514766')]:rect(x,915,48,48,col,16,EDGE);icon(kind,x+13,928,size=22)
 for x,t in [(655,'F'),(712,'C'),(769,'T')]:
  rect(x,915,40,38,CARD,12);label(x+20,942,t,17,anchor='middle');rect(x+18,958,5,3,ACCENT,2)
 for x,kind in [(834,'search'),(896,'running')]:rect(x,915,48,48,CARD,16,EDGE);icon(kind,x+13,928,size=22)
 label(32,947,'Nexus · native desktop',11,MUTED);label(1240,947,'Control center',12)
def finish(name):
 label(32,992,'0.7.0 EXPERIENCE DESIGN REFERENCE · Illustrative data · Not a Windows screenshot',9,MUTED)
 add('</g></svg>');path=out/name;path.write_text('\n'.join(svg));print(path)

def toggle(x,y,title,on=True,caption=None):
 label(x,y,title,14)
 rect(x+425,y-15,42,22,ACCENT if on else CARD,11,EDGE)
 rect(x+449 if on else x+429,y-11,14,14,'#171C29' if on else MUTED,7)
 label(x,y+24,caption or ('On' if on else 'Off'),12,MUTED)
def personalize():
 add('<g clip-path="url(#viewport)">')
 label(320,178,'Make Nexus yours.',30,weight=600)
 label(320,211,'Choose your mood, shape the desktop, and keep what matters within reach.',14,MUTED)
 label(320,245,'AURA MOODS',11,MUTED,spacing=1)
 for i,(title,caption,accent,secondary,canvas) in enumerate([
   ('Pearl · Current','Iris & charcoal',ACCENT,TEAL,'#121722'),
   ('Lagoon','Deep teal','#8DDFD3','#AFBEF6','#101C22'),
   ('Graphite','Cool blue','#AACCF4','#C3BBE8','#121820')]):
  x=320+i*366;rect(x,263,354,128,CARD,20,ACCENT if i==0 else EDGE)
  for j,col in enumerate([accent,secondary,canvas]):rect(x+20+j*31,283,24,24,col,12,EDGE)
  label(x+20,337,title,17);label(x+20,366,caption,12,MUTED)
 for x in [320,872]:rect(x,411,536,590,CARD,22,EDGE)
 label(340,450,'Your desktop',21,weight=600)
 for i,(title,caption) in enumerate([
   ('Open desktop canvas','Floating desktop'),('Desktop widgets','On'),
   ('Clock and date','On'),('Personal space card','On'),('Home essentials','On'),('Home quick notes','On')]):toggle(340,493+i*67,title,True,caption)
 label(892,450,'Feel & interaction',21,weight=600)
 toggle(892,493,'Native glass',False,'Pearl surfaces')
 toggle(892,565,'Reduced effects',False,'Off')
 toggle(892,637,'Compact dock',False,'Comfortable icons')
 label(892,710,'Clock format',13)
 rect(892,726,496,46,'#141B29',12,EDGE);label(908,756,'24-hour · 14:56',14);label(1362,755,'⌄',18,MUTED)
 label(892,808,'Glass follows Windows availability.',12,MUTED)
 label(892,831,'High contrast and reduced effects use simpler surfaces.',12,MUTED)
 pill(892,851,186,'Open control center')
 add('</g>')
start('Nexus Shell 0.7.0 — Personalize design reference');frame();personalize();finish('Nexus-Experience-preview.svg')
start('Nexus Shell 0.7.0 — categorized search design reference');frame();personalize()
rect(0,80,1440,900,'#090E18',opacity=.76)
rect(380,96,680,590,'#1B2433',26,EDGE)
rect(404,120,560,56,'#141B29',12,EDGE);icon('search',422,138,ACCENT,21)
label(457,155,'Search your orbit…',18,MUTED);label(991,154,'Esc',14,MUTED)
x=404
for title,width in [('All',40),('Apps',52),('Saved',61),('Workspaces',101),('Actions',71),('Tasks',57),('Windows',81)]:
 if title=='All':rect(x,190,width,34,'#303D50',12)
 label(x+width/2,212,title,12,ACCENT if title=='All' else TEXT,anchor='middle');x+=width+4
label(404,253,'RECENT & SUGGESTED',10,MUTED,spacing=1);label(1036,253,'30 shown',11,MUTED,anchor='end')
for i,(title,subtitle,kind) in enumerate([
 ('Firefox','Recent · Windows app','browser'),('ICT learning resources','Recent · Study · Saved link','files'),
 ('Study workspace','Recent · Make room for your A/L studies','focus'),('Home','Your personal desktop','home'),
 ('Explore','Saved links, files and folders','files'),('Control center','Appearance and Windows settings','settings')]):
 y=272+i*60
 if i==0:rect(404,y,632,56,'#303D50',14)
 rect(418,y+10,36,36,CARD,12);icon(kind,427,y+19,ACCENT,18)
 label(470,y+26,title,14);label(470,y+44,subtitle,11,MUTED)
label(404,662,'↑ ↓ choose     Enter open     Esc close',11,MUTED)
label(380,737,'YOUR ORBIT, EASIER TO REACH',11,ACCENT,spacing=1.4)
label(380,773,'Filter apps, saved things, workspaces, actions, tasks or open windows.',14)
label(380,807,'Recent-item shortcuts can be cleared or disabled in Personalize.',13,MUTED)
finish('Nexus-Experience-search.svg')
