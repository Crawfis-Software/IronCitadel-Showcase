"""Labels the top-down render with room names from Map/map.json and draws the walked routes from Checks/walk_trace.csv.
World -> pixel uses the same framing as Shots.Overview (orthographic, centred on the level bounds)."""
import csv, json, sys
from PIL import Image, ImageDraw, ImageFont

raw, out_clean, out_walks = sys.argv[1], sys.argv[2], sys.argv[3]
m = json.load(open('Map/map.json'))
im = Image.open(raw).convert('RGB')
W, H = im.size
xs = [p[0] for r in m['rooms'] for o in r['floor'] for p in o]
zs = [p[1] for r in m['rooms'] for o in r['floor'] for p in o]
mn = (min(xs) - 0.2, min(zs) - 0.2); mx = (max(xs) + 0.2, max(zs) + 0.2)
c = ((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2)
aspect = W / H
size = max((mx[1] - mn[1]) / 2 + 2, ((mx[0] - mn[0]) / 2 + 2) / aspect)
s = H / (2 * size)

def px(x, z):
    return (W / 2 + (x - c[0]) * s, H / 2 - (z - c[1]) * s)

font = ImageFont.load_default(size=30)
small = ImageFont.load_default(size=22)

def label(d, x, z, text, f=font):
    X, Y = px(x, z)
    b = d.textbbox((0, 0), text, font=f)
    w, h = b[2] - b[0], b[3] - b[1]
    d.rectangle([X - w / 2 - 8, Y - h / 2 - 6, X + w / 2 + 8, Y + h / 2 + 8], fill=(15, 10, 8))
    d.text((X - w / 2, Y - h / 2 - b[1] + 1), text, font=f, fill=(255, 240, 215))

def annotate(img, walks):
    d = ImageDraw.Draw(img)
    if walks:
        colours = {'W1': (80, 220, 255), 'W2': (120, 255, 120), 'W3': (255, 210, 60), 'W4': (255, 120, 255), 'W4b': (255, 120, 255), 'W5': (255, 90, 70)}
        pts = {}
        for row in csv.DictReader(open('Checks/walk_trace.csv')):
            if row['leg'] in colours:
                pts.setdefault(row['leg'], []).append(px(float(row['x']), float(row['z'])))
        for leg, p in pts.items():
            if len(p) > 1: d.line(p, fill=colours[leg], width=6, joint='curve')
    for r in m['rooms']:
        o = r['floor'][0]
        cx = sum(p[0] for p in o) / len(o); cz = sum(p[1] for p in o) / len(o)
        name = r['name'] if r['kind'] != 'extra' else r['name'] + ' (extra)'
        if r['kind'] == 'dining hall': cz += 3.5
        label(d, cx, cz, name)
    sx, sz = m['start']['position'][0], m['start']['position'][2]
    X, Y = px(sx, sz)
    d.polygon([(X, Y - 26), (X - 16, Y + 12), (X + 16, Y + 12)], fill=(255, 255, 255), outline=(0, 0, 0))
    label(d, sx, sz - 1.6, 'START', small)
    # legend
    lines = [((230, 40, 30), 'ranged defender'), ((240, 140, 20), 'melee defender'), ((170, 40, 230), 'the master'), ((240, 210, 70), 'pickup (best gear, hoard)')]
    if walks:
        lines += [((80, 220, 255), 'W1 the way round'), ((120, 255, 120), 'W2 up to the gallery'), ((255, 210, 60), 'W3 into the gun room from behind'),
                  ((255, 120, 255), 'W4 the hidden strong room and back'), ((255, 90, 70), 'W5 on to the master\'s chair')]
    x0, y0 = W - 560, H - 60 - 40 * len(lines)
    d.rectangle([x0 - 20, y0 - 60, W - 20, H - 30], fill=(15, 10, 8))
    d.text((x0, y0 - 50), 'Hollowmere Manor - top down, ceilings hidden', font=small, fill=(255, 240, 215))
    for i, (col, t) in enumerate(lines):
        y = y0 + i * 40
        d.ellipse([x0, y, x0 + 26, y + 26], fill=col)
        d.text((x0 + 40, y), t, font=small, fill=(255, 240, 215))
    d.text((40, H - 70), 'scale 34 px = 1 m; north is up; tall rooms: Dining Hall 9 m, Ballroom 12 m, Chapel 8 m', font=small, fill=(200, 190, 170))
    return img

annotate(im.copy(), False).save(out_clean)
annotate(im.copy(), True).save(out_walks)
print('ok', W, H, round(s, 2))
