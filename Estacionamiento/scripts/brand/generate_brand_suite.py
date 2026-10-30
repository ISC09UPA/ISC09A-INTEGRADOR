import os
import shutil
from PIL import Image, ImageDraw, ImageFont

def render_delta_apex_mark(size=1024, scale=4):
    """
    Renders pure white Delta Apex mark on 100% transparent RGBA.
    """
    super_size = size * scale
    img = Image.new("RGBA", (super_size, super_size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    
    # Mathematical coordinates scaled to super_size
    s = super_size / 512.0
    poly_left = [
        (246 * s, 92 * s),
        (82 * s, 368 * s),
        (194 * s, 318 * s),
        (246 * s, 389 * s)
    ]
    poly_right = [
        (266 * s, 92 * s),
        (430 * s, 368 * s),
        (318 * s, 318 * s),
        (266 * s, 389 * s)
    ]
    d.polygon(poly_left, fill=(255, 255, 255, 255))
    d.polygon(poly_right, fill=(255, 255, 255, 255))
    
    return img.resize((size, size), Image.Resampling.LANCZOS)

def render_horizontal_lockup(w=1600, h=400, scale=3):
    super_w, super_h = w * scale, h * scale
    img = Image.new("RGBA", (super_w, super_h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    
    # Scale emblem
    mark_super_size = int(super_h * 0.72)
    mark = render_delta_apex_mark(size=mark_super_size, scale=2)
    # Paste mark on left
    mark_x = int(super_w * 0.05)
    mark_y = int((super_h - mark_super_size) / 2)
    img.paste(mark, (mark_x, mark_y), mark)
    
    # Render typography using high quality PIL fonts
    font_size_title = int(94 * scale * (super_h / (400 * scale)))
    font_size_sub = int(24 * scale * (super_h / (400 * scale)))
    
    # Try finding system sans-serif font (Segoe UI / Arial on Windows)
    font_title = None
    font_sub = None
    for font_name in ["segoeui.ttf", "arial.ttf", "calibri.ttf"]:
        try:
            font_title = ImageFont.truetype(font_name, font_size_title)
            font_sub = ImageFont.truetype(font_name, font_size_sub)
            break
        except:
            continue
            
    if font_title is None:
        font_title = ImageFont.load_default()
        font_sub = ImageFont.load_default()
        
    text_x = mark_x + mark_super_size + int(super_w * 0.04)
    text_y_title = int(super_h * 0.28)
    text_y_sub = int(super_h * 0.62)
    
    # Draw letter-spaced wordmark
    def draw_tracked_text(draw_ctx, text, x, y, font, fill, tracking):
        curr_x = x
        for char in text:
            draw_ctx.text((curr_x, y), char, font=font, fill=fill)
            bbox = font.getbbox(char)
            char_w = (bbox[2] - bbox[0]) if bbox else 20
            curr_x += char_w + tracking
            
    draw_tracked_text(d, "AEROPARK", text_x, text_y_title, font_title, (255, 255, 255, 255), int(18 * scale))
    draw_tracked_text(d, "AIRPORT MOBILITY · CASHLESS", text_x + 4, text_y_sub, font_sub, (255, 255, 255, 160), int(10 * scale))
    
    return img.resize((w, h), Image.Resampling.LANCZOS)

def render_stacked_lockup(size=800, scale=3):
    super_size = size * scale
    img = Image.new("RGBA", (super_size, super_size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    
    # Center mark on top
    mark_super_size = int(super_size * 0.44)
    mark = render_delta_apex_mark(size=mark_super_size, scale=2)
    mark_x = (super_size - mark_super_size) // 2
    mark_y = int(super_size * 0.14)
    img.paste(mark, (mark_x, mark_y), mark)
    
    font_size_title = int(64 * scale)
    font_size_sub = int(20 * scale)
    font_title = None
    font_sub = None
    for font_name in ["segoeui.ttf", "arial.ttf", "calibri.ttf"]:
        try:
            font_title = ImageFont.truetype(font_name, font_size_title)
            font_sub = ImageFont.truetype(font_name, font_size_sub)
            break
        except:
            continue
            
    if font_title is None:
        font_title = ImageFont.load_default()
        font_sub = ImageFont.load_default()
        
    text_y_title = int(super_size * 0.64)
    text_y_sub = int(super_size * 0.76)
    
    # Calculate width of tracked text to center
    def get_tracked_w(text, font, tracking):
        total_w = 0
        for char in text:
            bbox = font.getbbox(char)
            char_w = (bbox[2] - bbox[0]) if bbox else 20
            total_w += char_w + tracking
        return total_w - tracking
        
    def draw_tracked_text_centered(draw_ctx, text, center_x, y, font, fill, tracking):
        total_w = get_tracked_w(text, font, tracking)
        curr_x = center_x - total_w // 2
        for char in text:
            draw_ctx.text((curr_x, y), char, font=font, fill=fill)
            bbox = font.getbbox(char)
            char_w = (bbox[2] - bbox[0]) if bbox else 20
            curr_x += char_w + tracking

    draw_tracked_text_centered(d, "AEROPARK", super_size // 2, text_y_title, font_title, (255, 255, 255, 255), int(16 * scale))
    draw_tracked_text_centered(d, "AIRPORT MOBILITY", super_size // 2, text_y_sub, font_sub, (255, 255, 255, 150), int(10 * scale))
    
    return img.resize((size, size), Image.Resampling.LANCZOS)

def main():
    dirs = [
        r"C:\Users\icrye\OneDrive\Desktop\Integrador\frontend\customer-mobile\src\assets\logo",
        r"C:\Users\icrye\OneDrive\Desktop\Integrador\docs\assets",
        r"C:\Users\icrye\.gemini\antigravity-ide\brain\5c72ef56-ca2c-419c-9cb0-6443b8150983"
    ]
    
    print("Generating Delta Apex family...")
    mark_1024 = render_delta_apex_mark(1024)
    mark_512 = mark_1024.resize((512, 512), Image.Resampling.LANCZOS)
    mark_128 = mark_1024.resize((128, 128), Image.Resampling.LANCZOS)
    
    horiz_1600 = render_horizontal_lockup(1600, 400)
    stacked_800 = render_stacked_lockup(800)
    
    assets = {
        "aeropark_logo_mark_1024.png": mark_1024,
        "aeropark_logo_mark.png": mark_512,
        "aeropark_logo_mark_small.png": mark_128,
        "aeropark_logo_horizontal.png": horiz_1600,
        "aeropark_logo_stacked.png": stacked_800
    }
    
    for d in dirs:
        os.makedirs(d, exist_ok=True)
        for fname, img in assets.items():
            out_file = os.path.join(d, fname)
            img.save(out_file, "PNG")
            
    # Also create a dark presentation composite showcasing all 3 lockups on dark glass
    comp = Image.new("RGBA", (1600, 900), (14, 15, 17, 255))
    comp_draw = ImageDraw.Draw(comp)
    
    # Left Card: Stacked Lockup
    comp_draw.rounded_rectangle([80, 80, 680, 820], radius=24, fill=(24, 25, 28, 255), outline=(45, 48, 54, 255), width=1)
    stacked_scaled = stacked_800.resize((540, 540), Image.Resampling.LANCZOS)
    comp.paste(stacked_scaled, (110, 150), stacked_scaled)
    
    # Top Right Card: Horizontal Lockup
    comp_draw.rounded_rectangle([720, 80, 1520, 430], radius=24, fill=(24, 25, 28, 255), outline=(45, 48, 54, 255), width=1)
    horiz_scaled = horiz_1600.resize((720, 180), Image.Resampling.LANCZOS)
    comp.paste(horiz_scaled, (760, 160), horiz_scaled)
    
    # Bottom Right Card: Standalone Mark
    comp_draw.rounded_rectangle([720, 470, 1520, 820], radius=24, fill=(24, 25, 28, 255), outline=(45, 48, 54, 255), width=1)
    mark_scaled = mark_512.resize((260, 260), Image.Resampling.LANCZOS)
    comp.paste(mark_scaled, (1080 - 130, 515), mark_scaled)
    
    comp_path = os.path.join(r"C:\Users\icrye\.gemini\antigravity-ide\brain\5c72ef56-ca2c-419c-9cb0-6443b8150983", "aeropark_brand_suite_preview.jpg")
    comp.convert("RGB").save(comp_path, "JPEG", quality=95)
    print("Brand suite preview saved to", comp_path)

if __name__ == "__main__":
    main()
