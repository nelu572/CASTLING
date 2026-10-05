import argparse
from pathlib import Path

from PIL import Image, ImageDraw


DEST = Path(__file__).resolve().parents[2] / 'Assets/Sprite/Map/Background'
S = 3
SIZE = (896, 1280)
MAIN = (160, 155, 148, 255)
MID = (153, 148, 142, 255)
BASE = (151, 146, 140, 255)
CLEAR = (0, 0, 0, 0)


class Piece:
    def __init__(self, output_dir):
        self.output_dir = output_dir
        self.image = Image.new('RGBA', (SIZE[0] * S, SIZE[1] * S), CLEAR)
        self.draw = ImageDraw.Draw(self.image)

    def rect(self, box, color=MAIN, radius=0):
        b = tuple(round(v * S) for v in box)
        self.draw.rounded_rectangle(b, radius=radius * S, fill=color)

    def ellipse(self, box, color=MAIN):
        self.draw.ellipse(tuple(round(v * S) for v in box), fill=color)

    def poly(self, points, color=MAIN):
        self.draw.polygon([(round(x * S), round(y * S)) for x, y in points], fill=color)

    def save(self, name):
        output_path = self.output_dir / 'Landmarks' / name
        output_path.parent.mkdir(parents=True, exist_ok=True)
        self.image.resize(SIZE, Image.Resampling.LANCZOS).save(output_path, optimize=True)


def foot(p, width=660):
    left = (SIZE[0] - width) // 2
    p.rect((left + 25, 1030, left + width - 25, 1135), MID, 30)
    p.rect((left, 1100, left + width, 1215), BASE, 36)


def pawn(output_dir):
    p = Piece(output_dir)
    p.ellipse((305, 235, 591, 521))
    p.rect((370, 490, 526, 632), MAIN, 28)
    p.ellipse((248, 565, 648, 755), MID)
    p.poly([(345, 670), (551, 670), (637, 1070), (259, 1070)], MAIN)
    foot(p, 640)
    p.save('BG_PawnLandmark.png')


def bishop(output_dir):
    p = Piece(output_dir)
    p.ellipse((415, 125, 481, 191))
    p.poly([(448, 194), (543, 278), (612, 428), (607, 535),
            (551, 654), (345, 654), (289, 535), (284, 428), (353, 278)])
    p.poly([(572, 310), (604, 348), (357, 575), (324, 537)], CLEAR)
    p.rect((333, 624, 563, 714), MID, 30)
    p.poly([(368, 690), (528, 690), (621, 1060), (275, 1060)], MAIN)
    foot(p, 640)
    p.save('BG_BishopLandmark.png')


def knight(output_dir):
    p = Piece(output_dir)
    p.poly([(185, 555), (232, 480), (326, 424), (376, 343),
            (415, 225), (466, 318), (532, 276), (575, 378),
            (629, 427), (581, 485), (638, 550), (590, 615),
            (620, 1060), (275, 1060), (330, 741), (276, 657),
            (216, 627)], MAIN)
    p.poly([(575, 378), (629, 427), (581, 485), (638, 550),
            (590, 615), (620, 1060), (558, 1060), (540, 640)], MID)
    p.ellipse((352, 414, 384, 446), CLEAR)
    p.ellipse((229, 557, 251, 579), CLEAR)
    foot(p, 670)
    p.save('BG_KnightLandmark.png')


def queen(output_dir):
    p = Piece(output_dir)
    tips = [(200, 267), (322, 191), (448, 221), (574, 191), (696, 267)]
    for x, y in tips:
        p.ellipse((x - 34, y - 34, x + 34, y + 34))
    p.poly([(198, 275), (296, 485), (322, 204), (392, 476),
            (448, 234), (504, 476), (574, 204), (600, 485),
            (698, 275), (613, 633), (283, 633)])
    p.rect((278, 600, 618, 690), MID, 34)
    p.poly([(347, 672), (549, 672), (635, 1060), (261, 1060)], MAIN)
    foot(p, 690)
    p.save('BG_QueenLandmark.png')


def king(output_dir):
    p = Piece(output_dir)
    p.rect((414, 125, 482, 348), MAIN, 20)
    p.rect((348, 187, 548, 257), MAIN, 20)
    p.poly([(337, 385), (285, 320), (298, 563), (364, 661),
            (532, 661), (598, 563), (611, 320), (559, 385),
            (448, 335)], MAIN)
    p.rect((333, 627, 563, 711), MID, 30)
    p.poly([(350, 690), (546, 690), (629, 1060), (267, 1060)], MAIN)
    foot(p, 680)
    p.save('BG_KingLandmark.png')


def tiles(output_dir, patterns=('A', 'B')):
    colors = [(160, 155, 148, 200), (151, 146, 140, 200)]
    for pattern, name, order in [('A', 'BG_CheckerTile_A.png', (0, 1, 1, 0)),
                                 ('B', 'BG_CheckerTile_B.png', (1, 0, 0, 1))]:
        if pattern not in patterns:
            continue
        image = Image.new('RGBA', (128, 128), CLEAR)
        draw = ImageDraw.Draw(image)
        for y in range(2):
            for x in range(2):
                draw.rectangle((x * 64, y * 64, x * 64 + 63, y * 64 + 63),
                               fill=colors[order[y * 2 + x]])
        output_path = output_dir / 'Patterns' / name
        output_path.parent.mkdir(parents=True, exist_ok=True)
        image.save(output_path, optimize=True)


GENERATORS = {
    'pawn': pawn,
    'bishop': bishop,
    'knight': knight,
    'queen': queen,
    'king': king,
    'checker-a': lambda output_dir: tiles(output_dir, ('A',)),
    'checker-b': lambda output_dir: tiles(output_dir, ('B',)),
}


def main(argv=None):
    parser = argparse.ArgumentParser(description='Generate chess background PNGs.')
    parser.add_argument(
        '--output-dir', type=Path, default=DEST,
        help='Output root containing Landmarks and Patterns (default: %(default)s).')
    parser.add_argument(
        '--only', choices=tuple(GENERATORS), nargs='+',
        help='Generate only these outputs; omit to generate all seven PNGs.')
    args = parser.parse_args(argv)

    for name in dict.fromkeys(args.only or GENERATORS):
        GENERATORS[name](args.output_dir)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
