"""Generate the original static SVG icon set used by the native desktop.

Only Direct2D-compatible static paths, shapes, gradients and opacity are used.
No embedded bitmap, external URL, font, animation, script or SVG filter.
"""
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / 'src/Nexus.Shell/Assets/Icons'
OUT.mkdir(parents=True, exist_ok=True)
def icon(name, top, bottom, art, tile=True):
    base = '''<rect x="7" y="11" width="66" height="66" rx="17" fill="#202943" opacity=".16"/><rect x="6" y="6" width="68" height="68" rx="17" fill="url(#tile)"/><rect x="7" y="7" width="66" height="66" rx="16" fill="none" stroke="#ffffff" stroke-opacity=".36"/><path d="M22 8H58C66 8 72 14 72 22V32C51 24 29 28 8 36V22C8 14 14 8 22 8Z" fill="#ffffff" opacity=".14"/>''' if tile else ''
    svg = '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80"><defs><linearGradient id="tile" x1="0" y1="0" x2="0" y2="1"><stop stop-color="'+top+'"/><stop offset="1" stop-color="'+bottom+'"/></linearGradient><linearGradient id="paper" x1="0" y1="0" x2="0" y2="1"><stop stop-color="#ffffff"/><stop offset="1" stop-color="#d9e7fa"/></linearGradient><linearGradient id="folder" x1="0" y1="0" x2="0" y2="1"><stop stop-color="#7cdbff"/><stop offset="1" stop-color="#309ddd"/></linearGradient></defs>'+base+art+'</svg>\n'
    (OUT/(name+'.svg')).write_text(svg,encoding='utf-8')

icon('Nexus','#8ebafb','#5368ce','<circle cx="40" cy="40" r="17" fill="#e6eeff" opacity=".96"/><path d="M18 44C24 57 58 51 63 33" fill="none" stroke="#b8f1fc" stroke-width="5" stroke-linecap="round"/><path d="M22 26C40 12 63 30 61 48" fill="none" stroke="#ffffff" stroke-opacity=".88" stroke-width="3" stroke-linecap="round"/><circle cx="61" cy="33" r="4" fill="#ffd9ae"/>')
icon('Files','#91ddff','#3c9bdc','<path d="M8 31H28L34 24H67Q73 24 73 31V66Q73 71 67 71H13Q7 71 7 65Z" fill="#20456d" opacity=".16"/><path d="M7 25Q7 20 13 20H29L36 26H68Q73 26 73 32V61H7Z" fill="#369cdb"/><rect x="10" y="30" width="60" height="30" rx="3" fill="#c7edff"/><path d="M5 36Q5 32 11 32H69Q75 32 75 38L71 64Q70 68 65 68H15Q10 68 9 64Z" fill="url(#folder)"/><path d="M12 34H68" stroke="#d9f6ff" stroke-width="2" stroke-linecap="round"/>',False)
icon('Explore','#c2abf3','#8b6bcb','<path d="M20 29H33L38 34H59Q62 34 62 38V58Q62 61 58 61H22Q18 61 18 57V33Q18 29 20 29Z" fill="#f4eaff"/><path d="M21 37H59V56H21Z" fill="#d9c5f5"/><circle cx="48" cy="26" r="10" fill="#f4eaff"/><path d="M48 19L50 24L55 26L50 28L48 33L46 28L41 26L46 24Z" fill="#9273d6"/>')
icon('Study','#f4bd93','#e47d9c','<path d="M24 20H55Q60 20 60 26V61H24Q18 61 18 55V26Q18 20 24 20Z" fill="#fff5e8"/><path d="M24 20V61" stroke="#dba78c" stroke-width="2"/><path d="M48 20H54V43L51 39L48 43Z" fill="#df7089"/><path d="M32 33H45M32 41H44M32 49H48" stroke="#c8b8bd" stroke-width="2" stroke-linecap="round"/>')
icon('Apps','#fafbff','#dce5f4','<rect x="18" y="18" width="19" height="19" rx="6" fill="#729de4"/><rect x="43" y="18" width="19" height="19" rx="6" fill="#ae8dd6"/><rect x="18" y="43" width="19" height="19" rx="6" fill="#ea9eb2"/><rect x="43" y="43" width="19" height="19" rx="6" fill="#88c9bd"/>')
icon('Search','#e8eefa','#abbad9','<circle cx="35" cy="34" r="15" fill="#f5f8ff" opacity=".65" stroke="#627dac" stroke-width="4"/><path d="M46 46L61 61" stroke="#506f9f" stroke-width="7" stroke-linecap="round"/><path d="M28 26Q32 21 39 23" stroke="#ffffff" stroke-width="3" fill="none" stroke-linecap="round"/>')
icon('Windows','#84cbd7','#5895b5','<rect x="16" y="19" width="36" height="28" rx="5" fill="#c3e8f2"/><path d="M17 26H51" stroke="#76b5cf" stroke-width="2"/><rect x="29" y="34" width="35" height="28" rx="5" fill="#eefaff"/><path d="M30 41H63" stroke="#b0dce9" stroke-width="2"/>')
icon('Browser','#8bd2f7','#5590d3','<circle cx="40" cy="40" r="24" fill="#f5faff"/><circle cx="40" cy="40" r="20" fill="#7db5e5"/><path d="M40 21V25M40 55V59M21 40H25M55 40H59" stroke="#e9f9ff" stroke-width="2"/><path d="M29 51L36 36L51 29L44 44Z" fill="#fff9f1"/><path d="M36 36L51 29L44 44Z" fill="#ea819b"/><circle cx="40" cy="40" r="2.5" fill="#fff8f4"/>')
icon('Terminal','#4a536c','#242b42','<path d="M21 28L33 40L21 52M39 53H58" fill="none" stroke="#d6e9eb" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>')
gear=''.join(f'<rect x="36" y="15" width="8" height="15" rx="2" fill="#697a98" transform="rotate({a} 40 40)"/>' for a in range(0,360,45))
icon('Settings','#eef2f7','#b3bfce',gear+'<circle cx="40" cy="40" r="18" fill="#6a7d9b"/><circle cx="40" cy="40" r="11" fill="#d6e1ef"/><circle cx="40" cy="40" r="7" fill="#91a4c0"/>')
icon('Game','#a2d6b2','#61a48f','<path d="M28 29H52Q61 30 64 52Q65 64 58 61L48 54H32L22 61Q15 64 16 52Q19 30 28 29Z" fill="#f4fbf5"/><path d="M29 36V48M23 42H35" stroke="#769f99" stroke-width="4" stroke-linecap="round"/><circle cx="49" cy="39" r="3" fill="#e7b991"/><circle cx="56" cy="46" r="3" fill="#cb91ac"/>')
icon('Note','#f7d992','#e8b860','<path d="M22 12H59Q64 12 64 17V66Q64 70 59 70H22Q17 70 17 65V17Q17 12 22 12Z" fill="#42516d" opacity=".13"/><rect x="17" y="10" width="46" height="59" rx="7" fill="#fffdf3"/><path d="M17 17Q17 10 24 10H56Q63 10 63 17V25H17Z" fill="#f1c976"/><path d="M26 34H54M26 43H54M26 52H48" stroke="#d6cebb" stroke-width="2" stroke-linecap="round"/>',False)
icon('Document','#d2e5fa','#88b7e4','<path d="M24 11H48L63 26V66Q63 70 59 70H24Q18 70 18 64V17Q18 11 24 11Z" fill="#3a527a" opacity=".14"/><path d="M24 8H47L61 23V63Q61 67 57 67H24Q18 67 18 61V14Q18 8 24 8Z" fill="url(#paper)"/><path d="M47 8V19Q47 23 51 23H61Z" fill="#87b5e2"/><path d="M27 34H51M27 43H51M27 52H43" stroke="#88a8cd" stroke-width="2.5" stroke-linecap="round"/>',False)
print('Generated',len(list(OUT.glob('*.svg'))),'original static vector icons.')
