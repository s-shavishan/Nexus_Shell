"""Source-grounded Opal layout illustrations, not native WinUI screenshots.

Run: python scripts/opal-preview.py
Optional PNG rendering: install cairosvg and run with --png.
"""
from pathlib import Path
from html import escape
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs'
INK, MUTED, BLUE = '#1b273d', '#4b5b73', '#2454aa'
parts = []
def add(s): parts.append(s)
def box(x, y, w, h, fill='#f8faff', r=20, opacity=.92):
    add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" fill-opacity="{opacity}" stroke="#edf4ff" stroke-opacity=".65"/>')
def text(x, y, s, size=13, color=INK, weight=400, anchor='start'):
    add(f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" font-weight="{weight}" text-anchor="{anchor}">{escape(s)}</text>')
def button(x, y, w, s, primary=False):
    box(x,y,w,38,BLUE if primary else '#e1eafa',12)
    text(x+w/2,y+24,s,12,'white' if primary else INK,500,'middle')
def start(page):
    parts.clear()
    add('<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1000" viewBox="0 0 1600 1000">')
    add(f'<title>NEXUS 0.8.0 Opal {page} design reference</title>')
    add('''<defs><linearGradient id="sky" x2="1" y2="1"><stop stop-color="#243856"/><stop offset="1" stop-color="#244c61"/></linearGradient><linearGradient id="fold" x2="1" y2="1"><stop stop-color="#b8a4f4"/><stop offset=".38" stop-color="#8b73df"/><stop offset=".69" stop-color="#4b77c4"/><stop offset="1" stop-color="#63c7d4"/></linearGradient><linearGradient id="fold2" x2="1" y2="1"><stop stop-color="#99c9f3"/><stop offset=".38" stop-color="#6890d8"/><stop offset=".73" stop-color="#9a87da"/><stop offset="1" stop-color="#cbb7ee"/></linearGradient><linearGradient id="hero" x2="1" y2="1"><stop stop-color="#e0e9fc"/><stop offset="1" stop-color="#d8efed"/></linearGradient></defs>''')
    add('<g font-family="DejaVu Sans, Segoe UI, sans-serif">')
    box(0,0,1600,1000,'url(#sky)',0,1)
    add('<path d="M480-180C1010-130 740 270 1130 395C1500 515 1310 800 1810 1000L1700 1260C1000 1190 1170 655 825 530C475 405 745 40 480-180Z" fill="url(#fold)" opacity=".85"/>')
    add('<path d="M680-170C1210 70 780 325 1150 520C1470 690 1030 900 1510 1150L1180 1260C970 1070 1120 780 920 635C495 345 1000 185 680-170Z" fill="url(#fold2)" opacity=".85"/>')
    add('<path d="M806-85C1140 150 890 330 1195 560C1400 715 1180 965 1535 1120" fill="none" stroke="#e4f0ff" stroke-opacity=".45" stroke-width="1.5"/>')
    box(12,8,1576,42,'#e6edf9',13,.91)
    box(28,16,24,24,BLUE,7,1);text(40,34,'N',15,'white',600,'middle');text(65,34,'NEXUS',12,INK,600)
    for x, label in [(162,'Desktop'),(250,'Workspaces'),(365,'Explore'),(448,'Study'),(521,'Apps')]:
        text(x,34,label,12,BLUE if page==label else MUTED,500)
    text(1410,34,'Wed  22:29',12);text(1552,34,'•••',12)
    text(36,80,'‹   ›',20,'#f5f8ff');text(106,78,'NEXUS  /  '+page.upper()+'  /  PERSONAL',10,'#d2dded')
    text(1556,78,'⌕  Search your orbit     Ctrl K',12,'#f5f8ff',400,'end')
    box(537,920,526,66,'#e6edf9',22,.91)
    for i,(glyph,color) in enumerate([('N',BLUE),('▣','#355dc7'),('F','#237ea1'),('B','#7550ba'),('A','#3f4e64'),('⌕','#dbe5f5'),('▤','#dbe5f5')]):
        x=549+i*72;box(x,929,48,48,color,14,1);text(x+24,960,glyph,23,'white' if i<5 else BLUE,500,'middle')
    text(36,954,'Nexus · native desktop',10,'#d2dded')
def desktop():
    box(36,130,124,32);text(98,151,'Your orbit',11,BLUE,500,'middle')
    text(36,195,'NEXUS / OPAL',10,'#d2dded');text(36,229,'Wednesday, 7 October',14,'#d2dded')
    text(29,310,'22:29',73,'#f5f8ff',300);text(36,346,'Good evening, Shan.',15,'#f5f8ff')
    box(36,378,236,228);text(56,413,'Shan’s space',18,INK,500)
    for y,s,n in [(452,'Personal','4'),(491,'Study','6'),(530,'Build','3')]:
        text(56,y,s,13);text(250,y,n,11,MUTED,400,'end')
    text(56,576,'Capture a note',12,BLUE)
    box(36,625,236,188);text(56,660,'A moment of focus',13,MUTED);text(56,721,'25:00',44,INK,300)
    button(56,749,122,'Start focus',True);button(186,749,65,'Study')
    text(43,852,'Open my files ↗',12,'#f5f8ff');text(43,891,'Windows settings ↗',12,'#f5f8ff')
    for i,label in enumerate(['Personal','Study','Build','Game']):
        box(304+i*319,114,303,52,'#f8faff',18,.86);text(327+i*319,147,label,13,BLUE if i==0 else INK,500)
    box(304,186,1260,231,'url(#hero)',26,1)
    text(334,221,'YOUR PERSONAL DESKTOP',10,BLUE,500);text(334,269,'Your day, your space.',36,INK,600)
    text(334,303,'Your everyday essentials.',14,MUTED);text(334,334,'4 apps · 4 saved items',12,MUTED)
    button(334,356,140,'Open Personal',True);button(484,356,117,'Your apps');button(611,356,112,'Configure')
    add('<ellipse cx="1425" cy="300" rx="60" ry="60" fill="#34465c"/><ellipse cx="1425" cy="300" rx="78" ry="28" fill="none" stroke="#2454aa" stroke-width="2" transform="rotate(-32 1425 300)"/><ellipse cx="1425" cy="300" rx="76" ry="24" fill="none" stroke="#285f70" transform="rotate(34 1425 300)"/>')
    text(1425,324,'N',68,'#f5f8ff',300,'middle')
    box(304,437,622,224);text(328,473,'Your essentials',19,INK,600);text(900,473,'App Library',12,BLUE,400,'end')
    for i,(s,c) in enumerate([('Files','#237ea1'),('Browser','#355dc7'),('Build','#7550ba'),('Apps','#3f4e64')]):
        x=332+i*145;box(x+27,505,56,56,c,17,1);text(x+55,542,s[0],27,'white',400,'middle');text(x+55,595,s,13,INK,400,'middle')
    box(942,437,622,224);text(966,473,'In focus',19,INK,600);text(1538,473,'Study',12,BLUE,400,'end')
    for i,s in enumerate(['Finish one ICT lesson','Write a small summary']):
        box(966,500+i*46,20,20,'#e1eafa',6);text(1000,516+i*46,s,13)
    text(966,623,'0 sessions today · One thing at a time',12,MUTED)
    box(304,682,622,138);text(328,718,'Within reach',19,INK,600);text(900,718,'Explore',12,BLUE,400,'end')
    text(328,756,'Microsoft Learn',13);text(328,792,'Ideas for NEXUS',13)
    box(942,682,622,60);text(966,719,'›   Quick note',15)
    for i,s in enumerate(['My world','Entertainment','Study time']):
        box(304+i*424,840,408,48);text(328+i*424,870,s,14)
def explore():
    box(150,110,1300,786,'#e4edf8',28,.94)
    for x,c in [(174,'#ff5f57'),(200,'#febc2e'),(226,'#28c840')]: add(f'<circle cx="{x}" cy="134" r="6" fill="{c}"/>')
    text(800,139,'Explore',13,MUTED,500,'middle');text(1415,140,'☰',15,MUTED)
    box(150,158,170,706,'#dee8f6',0,.8);text(171,195,'WORKSPACE',10,MUTED)
    for i,s in enumerate(['Workspaces','Home','Explore','Study','Apps','Games','Activity']):
        y=223+i*45
        if s=='Explore':box(162,y-5,146,36,'#c7d7ef',12,.95)
        text(183,y+18,s,13,BLUE if s=='Explore' else INK,500)
    text(171,599,'YOUR SESSION',10,MUTED);text(183,644,'Windows',13);text(183,689,'Personalize',13)
    text(174,829,'Shan',13,INK,600);text(174,848,'Personal workspace',10,MUTED)
    x=344;text(x,203,'Study',28,INK,600);text(x,229,'Resources for the things you are learning.',12,MUTED)
    for i,(label,w) in enumerate([('Personal',108),('Study',94),('Build',88)]):
        button(x+[0,120,226][i],248,w,label,i==1)
    box(x,303,1082,50);text(x+18,335,'Paste a link or jot down a note…',13,MUTED);button(1308,309,78,'Save',True);text(1410,335,'+',22,MUTED)
    box(x,370,742,40);text(x+16,396,'⌕  Search this space',13,MUTED);button(1098,371,158,'All collections');text(1290,397,'☆    ▦    ☷',18,BLUE)
    cards=[('Link','Microsoft Learn','An introduction to C# and .NET.','Resources'),('Note','Ideas for NEXUS','Make the desktop feel calm.','Ideas'),('Folder','A/L resources','The references I use every week.','Notes'),('Note','One thing for today','Finish one ICT lesson.','Focus'),('Note','Board design notes','Links, files and ideas in one place.','References'),('Link','MDN Web Docs','Learn one page at a time.','Resources')]
    for i,(kind,title,note,collection) in enumerate(cards):
        cx=x+i%3*258;cy=431+i//3*214
        box(cx,cy,244,198);box(cx+16,cy+16,34,34,'#d9e4f7',10);text(cx+33,cy+40,kind[0],18,BLUE,500,'middle')
        text(cx+228,cy+38,kind,10,MUTED,400,'end');text(cx+16,cy+81,title,15,INK,600)
        text(cx+16,cy+114,note,11,MUTED);text(cx+16,cy+176,collection,11,BLUE)
    box(1160,431,266,412,'#dce7f5',20,.85);text(1179,466,'Ideas for NEXUS',18,INK,600);text(1406,466,'×',18,MUTED)
    text(1179,493,'Ideas · Note',11,MUTED);text(1179,518,'#design   #nexus',11,BLUE)
    box(1179,538,228,100);text(1193,565,'Make the desktop feel calm.',12);text(1193,591,'Keep every action within reach.',12)
    button(1179,657,228,'Edit details…');button(1179,705,228,'Add to favorites');button(1179,753,228,'Move to space…')
    text(1179,820,'Remove from Explore…',11,MUTED);text(344,850,'6 shown · 6 in this space',10,MUTED)
    text(174,884,'Study · 13 / 100 items · Stored on this PC',10,MUTED);text(1426,884,'NEXUS 0.8.0',10,MUTED,400,'end')
def finish(name):
    box(0,988,1600,12,'#243856',0,1)
    text(1580,998,'DESIGN REFERENCE · Sample content · Native Windows rendering unverified',9,'#f5f8ff',400,'end')
    add('</g></svg>')
    path=OUT/(name+'.svg');path.write_text('\n'.join(parts),encoding='utf-8')
    if '--png' in sys.argv:
        import cairosvg
        cairosvg.svg2png(url=str(path),write_to=str(OUT/(name+'.png')))

if __name__ == '__main__':
    start('Desktop');desktop();finish('Nexus-0.8.0-Desktop-reference')
    start('Explore');explore();finish('Nexus-0.8.0-Explore-reference')
    print('Generated Opal layout references; not native screenshots.')
