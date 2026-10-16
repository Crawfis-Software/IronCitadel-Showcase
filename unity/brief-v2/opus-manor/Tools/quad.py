import sys
from PIL import Image
out = sys.argv[1]; fs = sys.argv[2:]
ims = [Image.open(f).convert('RGB') for f in fs]
W, H = ims[0].size
s = Image.new('RGB', (W * 2, H * ((len(ims) + 1) // 2)))
for i, im in enumerate(ims): s.paste(im, ((i % 2) * W, (i // 2) * H))
s = s.resize((s.width * 3 // 4, s.height * 3 // 4))
s.save(out)
