import random, math, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

def solve(A,b):
    n=len(b)
    M=[row[:]+[b[i]] for i,row in enumerate(A)]
    for c in range(n):
        p=max(range(c,n),key=lambda r:abs(M[r][c])); M[c],M[p]=M[p],M[c]
        d=M[c][c]; M[c]=[v/d for v in M[c]]
        for r in range(n):
            if r!=c:
                f=M[r][c]; M[r]=[a-f*b2 for a,b2 in zip(M[r],M[c])]
    return [M[i][n] for i in range(n)]

def coeffs(dst_quad, src_size):
    W,H=src_size
    src=[(0,0),(W,0),(W,H),(0,H)]
    A=[];b=[]
    for (x,y),(u,v) in zip(dst_quad,src):
        A.append([x,y,1,0,0,0,-u*x,-u*y]); b.append(u)
        A.append([0,0,0,x,y,1,-v*x,-v*y]); b.append(v)
    return solve(A,b)

def paper(w=1700,h=2400,lines=34,receipt=False):
    im=Image.new('RGB',(w,h),(238,235,226)); d=ImageDraw.Draw(im)
    big=ImageFont.load_default(size=90); mid=ImageFont.load_default(size=44)
    d.text((120,110),"HOP DONG DICH VU" if not receipt else "CUA HANG ABC",fill=(25,25,30),font=big)
    d.line((120,240,w-120,240),fill=(30,30,30),width=6)
    rnd=random.Random(w)
    for i in range(lines):
        y=320+i*(h-460)//lines; x=120
        while x<w-140:
            ww=rnd.randint(60,260)
            if x+ww>w-120: ww=w-120-x
            d.rectangle((x,y,x+ww,y+18),fill=(40,40,45)); x+=ww+rnd.randint(25,45)
    d.text((120,h-120),"Ky ten: ____________",fill=(20,20,60),font=mid)
    return im

def scene(name,bg,quad,size=(4000,3000),paper_im=None,shadow=0.0,blur=1.2,exif=1,noise=10,stripes=False):
    W,H=size
    im=Image.new('RGB',size,bg); d=ImageDraw.Draw(im)
    if stripes:
        for y in range(0,H,7):
            k=int(14*math.sin(y*0.05)+random.Random(y).randint(-6,6))
            d.line((0,y,W,y),fill=tuple(max(0,min(255,c+k)) for c in bg),width=7)
    p=paper_im or paper()
    c=coeffs(quad,p.size)
    warped=p.transform(size,Image.PERSPECTIVE,c,Image.BICUBIC)
    mask=Image.new('L',p.size,255).transform(size,Image.PERSPECTIVE,c,Image.BILINEAR)
    sh=mask.filter(ImageFilter.GaussianBlur(30)).point(lambda v:int(v*0.45))
    im=Image.composite(Image.new('RGB',size,(0,0,0)),im,ImageChops.offset(sh,25,35))
    im.paste(warped,(0,0),mask)
    if shadow:
        grad=Image.linear_gradient('L').rotate(90).resize(size)   # rotate BEFORE resizing: no corner artifacts
        im=Image.composite(im,Image.new('RGB',size,(0,0,0)),grad.point(lambda v:int(255*(1-shadow)+v*shadow)))
    im=im.filter(ImageFilter.GaussianBlur(blur))
    if noise:
        im=ImageChops.add(im,Image.effect_noise(size,noise).convert('RGB'),scale=1.0,offset=-128)
    ex=Image.Exif(); ex[0x0112]=exif
    im.save(name,quality=88,exif=ex); print(name)

scene("sc1_dark_desk.jpg",(58,44,36),[(720,300),(3260,330),(3200,2700),(760,2660)],stripes=True)
scene("sc2_light_perspective.jpg",(205,198,186),[(1000,240),(2950,300),(3500,2750),(420,2620)],shadow=0.30)
scene("sc3_cut_bottom.jpg",(70,80,92),[(600,260),(3350,200),(3450,3500),(520,3560)])
scene("sc4_receipt.jpg",(60,80,130),[(1500,700),(2500,740),(2440,2300),(1540,2260)],paper_im=paper(900,1700,lines=22,receipt=True),stripes=True)
