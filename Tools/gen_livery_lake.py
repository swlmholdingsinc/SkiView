"""Pass 16: livery for boats away from the home lake. Christopher's T_Livery_BaseColor carries "SWLM" + the "Ski View" script +
swoosh + a fictional registration; the script and swoosh belong to the home lake only. This keeps "SWLM" and the registration
and clears the script/swoosh band. Output: Assets/SkiView/Generated/T_Livery_SWLM.png (same 2048x256 RGBA layout)."""
from PIL import Image
import numpy as np, os
R = os.path.join(os.path.dirname(__file__), '..')
a = np.array(Image.open(os.path.join(R, 'Assets/SkiView/Boat/textures/T_Livery_BaseColor.png')).convert('RGBA'))
a[:, 500:1680, 3] = 0          # "Ski View" script + swoosh (x 540-1650 px); "SWLM" ends at ~440 px, the registration starts at ~1690 px
Image.fromarray(a).save(os.path.join(R, 'Assets/SkiView/Generated/T_Livery_SWLM.png')); print('wrote T_Livery_SWLM.png')
