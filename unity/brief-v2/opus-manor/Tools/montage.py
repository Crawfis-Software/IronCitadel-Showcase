import sys, os, glob
from PIL import Image, ImageDraw
src, out, start, count = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
fs = sorted(glob.glob(os.path.join(src, '*.png')))[start:start+count]
S = 160; cols = 10; rows = (len(fs)+cols-1)//cols
sheet = Image.new('RGB', (cols*S, rows*(S+14)), 'white'); d = ImageDraw.Draw(sheet)
for i, f in enumerate(fs):
    im = Image.open(f).convert('RGB').resize((S, S)); x = (i%cols)*S; y = (i//cols)*(S+14)
    sheet.paste(im, (x, y+14)); d.text((x+2, y+1), os.path.basename(f)[3:-4][:26], fill='black')
sheet.save(out); print(len(fs))
