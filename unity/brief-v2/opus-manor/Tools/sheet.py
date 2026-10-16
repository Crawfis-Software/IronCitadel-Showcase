import sys, os, glob
from PIL import Image, ImageDraw
src, out = sys.argv[1], sys.argv[2]
pat = sys.argv[3] if len(sys.argv) > 3 else '*.png'
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 4
fs = sorted(glob.glob(os.path.join(src, pat)))
W, H = 480, 270
rows = (len(fs) + cols - 1) // cols
s = Image.new('RGB', (cols * W, rows * (H + 16)), 'white'); d = ImageDraw.Draw(s)
for i, f in enumerate(fs):
    im = Image.open(f).convert('RGB').resize((W, H)); x = (i % cols) * W; y = (i // cols) * (H + 16)
    s.paste(im, (x, y + 16)); d.text((x + 3, y + 2), os.path.basename(f)[:-4], fill='black')
s.save(out); print(len(fs))
