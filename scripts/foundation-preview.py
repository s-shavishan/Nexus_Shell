"""Illustrations of the 1.3 desktop mode, using source wallpaper and icons.
Not Windows screenshots: fonts, z-order, work-area positioning and input need native review.
Run with Python and cairosvg; these are preview dependencies only.
"""
from pathlib import Path
from html import escape
import re, json
import cairosvg
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs'
TOKENS = json.loads((OUT / 'Solstice-palettes.json').read_text())['Solstice']['Tokens']
SOURCE = (ROOT / 'src/Nexus.Shell/UI/Desktop/DesktopSurface.cs').read_text()
PATHS = re.findall(r'"(M [^"\n]+)"', SOURCE)[:4]
COLORS = re.findall(r'"([A-F0-9]{8})"', re.search(r'"Solstice" => \[([^\]]+)\]', SOURCE)[1])
PARTS = []
W, H, BAR_Y = 1440, 900, 832

def color(key):
    value = TOKENS.get(key, key)
    return '#' + value[-6:], int(value[:2], 16) / 255

def rect(x, y, w, h, key='NexusCard', radius=10, border=False):
    c, opacity = color(key)
    PARTS.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{c}" fill-opacity="{opacity}"/>')
    if border:
        c, opacity = color('NexusBorder')
        PARTS.append(f'<rect x="{x+.5}" y="{y+.5}" width="{w-1}" height="{h-1}" rx="{radius}" fill="none" stroke="{c}" stroke-opacity="{opacity}"/>')

def text(x, y, value, size=13, key='NexusText', weight=400, anchor='start'):
    PARTS.append(f'<text x="{x}" y="{y}" font-size="{size}" fill="{color(key)[0]}" font-weight="{weight}" text-anchor="{anchor}">{escape(value)}</text>')

def icon(name, x, y, size=36):
    svg = (ROOT / 'src/Nexus.Shell/Assets/Icons' / (name + '.svg')).read_text()
    contents = svg[svg.index('>')+1:svg.rfind('</svg>')]
    for identifier in re.findall(r'id="([^"]+)"', contents):
        renamed = f'i{len(PARTS)}-{identifier}'
        contents = contents.replace(f'id="{identifier}"', f'id="{renamed}"').replace(f'url(#{identifier})', f'url(#{renamed})')
    PARTS.append(f'<svg x="{x}" y="{y}" width="{size}" height="{size}" viewBox="0 0 80 80">{contents}</svg>')

def wallpaper():
    PARTS.append('<defs>')
    for i, (first, last) in enumerate([(0, 3), (0, 1), (1, 2), (2, 3)]):
        PARTS.append(f'<linearGradient id="w{i}" x1="0%" y1="0%" x2="100%" y2="100%"><stop stop-color="{color(COLORS[first])[0]}"/><stop offset="1" stop-color="{color(COLORS[last])[0]}"/></linearGradient>')
    PARTS.append('</defs><g transform="scale(.9)">')
    for i, path in enumerate(PATHS):
        PARTS.append(f'<path d="{path}" fill="url(#w{i})"/>')
    PARTS.append('</g>')

def desktop():
    wallpaper()
    for i, (name, label) in enumerate([('Apps', 'Sections'), ('Files', 'My files'), ('Windows', 'Recycle Bin')]):
        y = 28 + i * 112
        icon(name, 46, y, 58)
        rect(28, y+65, 94, 24, 'NexusDesktopLabel', 5)
        text(75, y+81, label, 12, 'NexusDesktopText', anchor='middle')

def taskbar(sections):
    rect(0, BAR_Y, W, 68, 'NexusShell', 0)
    for i, (name, size) in enumerate([('Nexus', 36), ('Search', 32), ('Files', 36), ('Browser', 36), ('Terminal', 36), ('Settings', 36)]):
        x = 24 + i * 58 + (20 if i >= 2 else 0)
        icon(name, x, BAR_Y+16, size)
    if sections:
        rect(410, BAR_Y+16, 46, 48, 'NexusSelection', 9)
        icon('Files', 417, BAR_Y+22, 32)
        rect(429, BAR_Y+59, 8, 3, 'NexusAccent', 2)
    icon('Windows', 1148, BAR_Y+16, 34)
    icon('Settings', 1206, BAR_Y+16, 34)
    text(1370, BAR_Y+29, '9:31 AM', 12, anchor='end')
    text(1370, BAR_Y+46, 'Thu, 8 Oct', 10, 'NexusMuted', anchor='end')
    rect(1419, BAR_Y+20, 2, 28, 'NexusBorder', 0)

def files_window():
    x, y, w, h = 220, 76, 1000, 680
    rect(x+3, y+6, w, h, '301A1020', 7)
    rect(x, y, w, h, 'FF'+TOKENS['NexusPanel'][2:], 0, True)
    rect(x, y, w, 48, 'NexusSidebar', 0)
    for i,c in enumerate(['FFDC4C47','FFCA911B','FF259857']):
        PARTS.append(f'<circle cx="{x+26+i*36}" cy="{y+24}" r="5" fill="{color(c)[0]}"/>')
    text(x+w/2, y+30, 'My files · Nexus', 14, anchor='middle')
    for bx,label,bw in [(x+16,'Up',56),(x+80,'Refresh',84)]:
        rect(bx,y+64,bw,38,'NexusCard',8,True);text(bx+12,y+88,label,12)
    rect(x+172,y+64,626,38,'NexusInput',8,True)
    text(x+185,y+89,'C:\\Users\\Shan\\Documents',13)
    rect(x+810,y+64,174,38,'NexusInput',8,True);text(x+823,y+88,'Filter this folder',12,'NexusMuted')
    for i,label in enumerate(['My files','Desktop','Documents','Pictures','Music','Videos','Downloads','Recycle Bin','C:\\']):
        sy=y+120+i*43
        if label=='Documents':rect(x+16,sy-3,146,37,'NexusSelection',8)
        text(x+28,sy+20,label,13,'NexusSelectedText' if label=='Documents' else 'NexusText')
    names=[('Files','Nexus-Shell','Folder'),('Files','Study','Folder'),('Files','Personal','Folder'),('Document','Nexus-ideas.txt','2 KB · TXT'),('Document','Study-plan.json','4 KB · JSON'),('Document','Desktop-notes.pdf','180 KB · PDF')]
    for i,(name,title,detail) in enumerate(names):
        sy=y+119+i*53
        if i==0:rect(x+183,sy-2,801,49,'NexusSelection',8)
        icon(name,x+194,sy+7,28);text(x+238,sy+28,title,14,'NexusSelectedText' if i==0 else 'NexusText');text(x+842,sy+28,detail,11,'NexusMuted')
    text(x+18,y+h-73,'6 items · Double-click or press Enter to open',12,'NexusMuted')
    rect(x+w-154,y+h-52,138,36,'NexusCard',9,True);text(x+w-140,y+h-28,'New folder…',13)


def start_menu():
    x, y, w, h = 12, 280, 440, 540
    rect(x+3, y+5, w, h, '301A1020', 16)
    rect(x, y, w, h, 'FF'+TOKENS['NexusPanel'][2:], 16, True)
    text(x+20, y+40, 'Shan’s desktop', 17, weight=600)
    text(x+20, y+62, 'Your apps, within reach.', 12, 'NexusMuted')
    rect(x+20, y+81, w-40, 40, 'NexusInput', 10, True)
    text(x+33, y+107, 'Search your apps', 15, 'NexusMuted')
    for i, (name, label, detail) in enumerate([('Apps','Sections','Your workspaces, study and saved resources'),('Files','Files','System'),('Browser','Browser','Browser'),('Terminal','Terminal','Development'),('Settings','Settings','System')]):
        sy = y+140+i*61
        icon(name, x+31, sy, 30)
        text(x+77, sy+12, label, 13)
        text(x+77, sy+30, detail, 11, 'NexusMuted')
    for ix, label in [(20,'My files'), (118,'Personalize'), (248,'Session…')]:
        rect(x+ix, y+h-56, 88 if ix != 118 else 112, 34, 'NexusCard', 9, True)
        text(x+ix+12, y+h-34, label, 13)

def export(open_windows):
    PARTS.clear()
    PARTS.append('<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="936" viewBox="0 0 1440 936"><g font-family="Segoe UI, DejaVu Sans, sans-serif">')
    desktop()
    if open_windows: files_window()
    taskbar(open_windows)
    if open_windows: start_menu()
    rect(0, 900, W, 36, 'FFF8F5F1', 0)
    text(20, 923, 'NEXUS 1.3.0 · desktop mode · source-derived layout illustration with sample content', 12, 'FF625B59')
    text(1420, 923, 'Native Windows rendering pending', 11, 'FF625B59', anchor='end')
    PARTS.append('</g></svg>')
    name = 'Nexus-1.3.0-Files-and-Start-reference' if open_windows else 'Nexus-1.3.0-Desktop-reference'
    svg = OUT/(name+'.svg'); svg.write_text(''.join(PARTS))
    cairosvg.svg2png(url=str(svg), write_to=str(OUT/(name+'.png')))
    print(svg.name)

if __name__ == '__main__':
    export(False); export(True)
