import os
import base64
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
import numpy as np

# Load the real extracted brand cube
cube_path = r'C:\Users\cristobal1\Desktop\cube_extracted.png'
cube_img = Image.open(cube_path).convert('RGBA')

out_dir = r'C:\Users\cristobal1\Desktop\proposals_tier1'
os.makedirs(out_dir, exist_ok=True)

# Helper function to create base canvas with supersampling (1024x1024 downscaled to 512x512)
CANVAS_SIZE = 1024

def save_proposal(img, name):
    res = img.resize((512, 512), Image.Resampling.LANCZOS)
    path = os.path.join(out_dir, f'{name}.png')
    res.save(path, 'PNG')
    return path

print('Script loaded.')
