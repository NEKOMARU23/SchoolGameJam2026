# 資料用の図（四角と文字だけ）を作るスクリプト
# デザイナーさんの絵は使わない。絵がある場所は「立ち絵」などと書いた灰色の四角で表す
# 使い方: python Docs/tools/make_wireframes.py  → Docs/images/wire/ に PNG ができる
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "images", "wire")
os.makedirs(OUT, exist_ok=True)

FONT_B = "C:/Windows/Fonts/YuGothB.ttc"
FONT_M = "C:/Windows/Fonts/YuGothM.ttc"

# 色はゲームの見た目に似せず、中立の設計図の色にする（白・黒・灰色。説明は青、意味のある所だけ赤）
BG = (255, 255, 255)       # 図の背景（白）
LIME = (60, 60, 60)        # 枠・線（濃い灰色）
WHITE = (25, 25, 25)       # 文字（黒）
GRAY = (115, 115, 115)     # 小さな文字（灰色）
DARK = (255, 255, 255)     # 枠の中の塗り（白）
PLACE = (222, 222, 222)    # 絵の場所（灰色の四角）
DESK = (232, 232, 232)     # 机
RED = (210, 50, 50)        # 「AI判定」「NEW」など、意味のある所だけ
NOTE = (30, 90, 200)       # 図の説明の矢印・文字（青）

W, H = 1280, 720


def font(size, bold=True):
    return ImageFont.truetype(FONT_B if bold else FONT_M, size)


def canvas(w=W, h=H, color=BG):
    img = Image.new("RGB", (w, h), color)
    return img, ImageDraw.Draw(img)


def text(d, xy, s, size=22, color=WHITE, bold=True, anchor="la"):
    d.text(xy, s, font=font(size, bold), fill=color, anchor=anchor)


def box(d, rect, outline=LIME, fill=None, width=3, radius=0):
    if radius:
        d.rounded_rectangle(rect, radius=radius, outline=outline, fill=fill, width=width)
    else:
        d.rectangle(rect, outline=outline, fill=fill, width=width)


def placeholder(d, rect, label, size=18):
    """絵がある場所（灰色の四角と、何の絵かの文字）"""
    d.rectangle(rect, fill=PLACE)
    x0, y0, x1, y1 = rect
    text(d, ((x0 + x1) / 2, (y0 + y1) / 2), label, size, (95, 95, 95), anchor="mm")


def label(d, xy, s, size=20, color=WHITE, back=(255, 255, 255), line=LIME, anchor="la", pad=8):
    """文字の札（暗い四角に黄緑の線）"""
    f = font(size)
    l, t, r, b = d.textbbox(xy, s, font=f, anchor=anchor)
    d.rectangle((l - pad, t - pad, r + pad, b + pad), fill=back, outline=line, width=2)
    d.text(xy, s, font=f, fill=color, anchor=anchor)
    return (l - pad, t - pad, r + pad, b + pad)


def note(d, xy, s, size=18):
    """図の説明（黄色の文字）"""
    text(d, xy, s, size, NOTE)


def arrow(d, a, b, color=NOTE, width=3):
    d.line([a, b], fill=color, width=width)
    import math
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    for da in (2.6, -2.6):
        d.line([b, (b[0] + 16 * math.cos(ang + da), b[1] + 16 * math.sin(ang + da))], fill=color, width=width)


def title(d, s):
    text(d, (24, 18), s, 26, NOTE)


def marker(d, xy, n):
    """説明の番号（黄色の丸に数字）"""
    x, y = xy
    d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=NOTE)
    text(d, (x, y), str(n), 20, DARK, anchor="mm")


def save(img, name):
    img.save(os.path.join(OUT, name))
    print("作った:", name)


# ---------------- モニター画面 ----------------

def monitor(d, cx, cy, name=None, face=True, dark=False, tag=None, result=None):
    """モニター1つ（画面・名前プレート）。cx, cy は画面の中心"""
    w, h = 260, 130
    x0, y0 = cx - w / 2, cy - h / 2
    # 上の斜線の飾り
    for i in range(8):
        d.polygon([(x0 + i * 18, y0 - 6), (x0 + i * 18 + 10, y0 - 18), (x0 + i * 18 + 18, y0 - 18), (x0 + i * 18 + 8, y0 - 6)], fill=LIME)
    box(d, (x0, y0, x0 + w, y0 + h), LIME, (246, 246, 246) if face else (236, 236, 236))
    if face:
        placeholder(d, (cx - 55, y0 + 8, cx + 55, y0 + h - 4), "顔", 18)
    # 台と名前プレート
    d.line([(x0, y0 + h + 14), (x0 + w, y0 + h + 14)], fill=LIME, width=3)
    d.rectangle((x0 + w * 0.45, y0 + h + 18, x0 + w, y0 + h + 48), fill=LIME)
    if name:
        d.rectangle((x0 + w * 0.55, y0 + h + 16, x0 + w - 4, y0 + h + 50), fill=DARK, outline=(60, 60, 60), width=2)
        text(d, (x0 + w * 0.775, y0 + h + 33), name, 18, WHITE, anchor="mm")
    if dark:
        overlay = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 150))
        d._image.paste((10, 12, 16), (int(x0) + 3, int(y0) + 3, int(x0 + w) - 3, int(y0 + h) - 3)) if False else None
        d.rectangle((x0 + 3, y0 + 3, x0 + w - 3, y0 + h - 3), fill=(200, 200, 200))
        placeholder(d, (cx - 55, y0 + 8, cx + 55, y0 + h - 4), "顔（暗い）", 16) if face else None
        d.rectangle((cx - 55, y0 + 8, cx + 55, y0 + h - 4), fill=(175, 175, 175))
        text(d, (cx, cy + 40), "顔（暗い）", 14, (80, 80, 80), anchor="mm")
    if tag:
        label(d, (x0 + 12, y0 + 12), tag, 16)
    if result:
        color = RED if result == "AI判定" else WHITE
        label(d, (cx, cy - 4), result, 30, color, back=(255, 255, 255), line=(255, 255, 255), anchor="mm", pad=10)


def desk(d, note_gear=False, phone_hover=False, note_hover=False):
    d.polygon([(150, 545), (1130, 545), (1280, 720), (0, 720)], fill=DESK)
    # ノート
    if note_hover:
        d.rectangle((155, 560, 495, 712), fill=(200, 200, 200))
    placeholder(d, (170, 575 - (10 if note_hover else 0), 480, 715 - (10 if note_hover else 0)), "ノート（資料）", 20)
    if note_gear:
        label(d, (330, 668), "歯車（設定）", 18, anchor="mm")
    if note_hover:
        label(d, (505, 640), "資料", 22)
    # スマホ
    if phone_hover:
        d.rectangle((852, 568, 992, 690), fill=(200, 200, 200))
    placeholder(d, (866, 582 - (12 if phone_hover else 0), 978, 676 - (12 if phone_hover else 0)), "スマホ", 18)
    if phone_hover:
        label(d, (1005, 615), "スマホ", 22)


def header(d):
    d.rectangle((1090, 6, 1272, 90), fill=(245, 245, 245))
    for i, s in enumerate(["1日目", "ポイント: 0", "残り行動: 4 / 4"]):
        text(d, (1102, 14 + i * 25), s, 18, WHITE, bold=False)


def side_menu(d, labels=True):
    names = ["1日を終える", "今日をリセット", "ログ", "設定"]
    for i, n in enumerate(names):
        y = 34 + i * 62
        placeholder(d, (16, y - 20, 56, y + 20), "絵", 13)
        if labels:
            label(d, (78, y), n, 20, anchor="lm")


def monitor_before():
    img, d = canvas()
    for x in (40, 120, 200):
        placeholder(d, (x, 14, x + 44, 58), "絵", 13)
    note(d, (260, 26), "← アイコンに文字がない", 18)
    monitor(d, 300, 165, face=False)
    monitor(d, 980, 165, face=False)
    monitor(d, 435, 415, face=False)
    monitor(d, 840, 415, face=False)
    note(d, (560, 230), "モニターにだれがいるか分からない", 18)
    desk(d, note_gear=True)
    note(d, (520, 690), "← 歯車がノートの上にあって、1つの物に見える", 18)
    save(img, "monitor_before.png")


def monitor_after():
    img, d = canvas()
    side_menu(d)
    header(d)
    monitor(d, 435, 165, "レイジ")
    monitor(d, 840, 165, "カネル")
    monitor(d, 300, 415, "メリ")
    monitor(d, 980, 415, "ココア")
    desk(d)
    marker(d, (375, 120), 1)
    marker(d, (590, 263), 2)
    marker(d, (640, 290), 3)
    marker(d, (260, 34), 4)
    save(img, "monitor_after.png")


def monitor_hover():
    img, d = canvas(1280, 300)
    title(d, "スマホとノートにマウスを乗せたとき")
    sub = Image.new("RGB", (W, H), BG)
    sd = ImageDraw.Draw(sub)
    desk(sd, phone_hover=False)
    img.paste(sub.crop((0, 500, 640, 720)).resize((620, 213)), (10, 70))
    sub2 = Image.new("RGB", (W, H), BG)
    sd2 = ImageDraw.Draw(sub2)
    desk(sd2, phone_hover=True, note_hover=True)
    img.paste(sub2.crop((0, 500, 1280, 720)).resize((620, 107)), (650, 120))
    text(d, (20, 50), "ふつう（左半分の拡大）", 18, GRAY)
    text(d, (660, 50), "乗せたとき（少し持ち上がり、後ろが白く光り、名前が出る）", 18, GRAY)
    save(img, "monitor_hover.png")


def monitor_hover_simple():
    img, d = canvas(1100, 330)
    title(d, "スマホとノートにマウスを乗せたとき")
    # ふつう
    text(d, (40, 70), "ふつう", 20, GRAY)
    placeholder(d, (60, 120, 300, 290), "ノート", 20)
    placeholder(d, (360, 140, 460, 280), "スマホ", 18)
    arrow(d, (500, 205), (580, 205))
    # 乗せたとき
    text(d, (620, 70), "乗せたとき", 20, GRAY)
    d.rectangle((630, 106, 760, 286), fill=(200, 200, 200))
    placeholder(d, (644, 120, 746, 260), "スマホ", 18)
    label(d, (790, 190), "スマホ", 24, anchor="lm")
    note(d, (630, 296), "少し持ち上がる・後ろが白く光る（仮）・名前の札が出る", 16)
    save(img, "monitor_hover.png")


def monitor_final():
    img, d = canvas()
    side_menu(d)
    header(d)
    monitor(d, 435, 165, "レイジ", dark=True, result="AI判定")
    monitor(d, 840, 165, "カネル", tag="未判定")
    monitor(d, 300, 415, "メリ", tag="未判定")
    monitor(d, 980, 415, "ココア", dark=True, result="解放")
    desk(d)
    save(img, "monitor_final.png")


def morning_flow():
    img, d = canvas(1280, 300)
    title(d, "朝の会話の流れ（条件シートに「朝」の行がある日だけ）")
    steps = ["日が変わる演出\n「7日目」", "呼び出し音\n（素材ができたら）", "スマホの会話画面が\n自動で開く", "会話が終わると\n日が変わる演出", "モニター画面"]
    x = 30
    for i, s in enumerate(steps):
        box(d, (x, 110, x + 210, 220), LIME, (246, 246, 246), 3, 14)
        text(d, (x + 105, 165), s, 20, WHITE, anchor="mm")
        if i < len(steps) - 1:
            arrow(d, (x + 214, 165), (x + 246, 165))
        x += 250
    note(d, (30, 240), "スマホで話せる回数は減らない ／ 「今日をリセット」やロードでは流れない", 18)
    save(img, "morning_flow.png")


# ---------------- 取調室 ----------------

def room(d, buttons=True, notice=False, points=None, chat="「……本についてですか？」", mark=False):
    d.line([(20, 50), (60, 30)], fill=(60, 60, 60), width=6)
    text(d, (24, 60), "戻る", 16, GRAY)
    header(d)
    placeholder(d, (540, 20, 740, 540), "立ち絵", 28)
    placeholder(d, (1060, 120, 1280, 720), "資料（ボタン）", 20)
    if buttons:
        for i, s in enumerate(["拷問", "対話", "交渉", "友人について"]):
            x = 110 + i * 210 if i < 3 else 735
            w = 160 if i < 3 else 380
            box(d, (x, 305, x + w, 375), (60, 60, 60), DARK, 3)
            text(d, (x + w / 2, 340), s, 28, (60, 60, 60), anchor="mm")
    if notice:
        label(d, (615, 342), "今日はもう取り調べました", 26, anchor="mm", pad=12)
    if points:
        label(d, (830, 148), points, 34, LIME, anchor="mm", pad=12)
    # 会話の枠
    d.polygon([(275, 460), (510, 460), (565, 520), (1105, 520), (1105, 705), (180, 705), (180, 520), (275, 460)], fill=DARK, outline=(60, 60, 60))
    text(d, (360, 488), "ココア", 26, WHITE, anchor="mm")
    text(d, (215, 600), chat, 24, WHITE, bold=False, anchor="lm")
    if mark:
        f = font(24, False)
        right = d.textbbox((215, 600), chat, font=f, anchor="lm")[2]
        text(d, (right + 8, 603), "▼", 18, (25, 25, 25), anchor="lm")


def room_points():
    img, d = canvas()
    room(d, buttons=False, points="+20pt", chat="「本は、人と違って裏切りません」")
    note(d, (900, 190), "1.5秒ほど出て消える。0のときは出ない", 18)
    save(img, "room_points.png")


def room_after():
    img, d = canvas(1280, 400, (255, 255, 255))
    for i, (cap, notice) in enumerate([("行動を選んだ直後: ボタンが消えるだけ", False), ("モニターに戻って入り直したとき: 文が出る", True)]):
        sub, sd = canvas()
        room(sd, buttons=False, notice=notice, chat="" if notice else "「……本についてですか？」")
        img.paste(sub.resize((620, 349)), (10 + i * 640, 10))
        text(d, (16 + i * 640, 384), cap, 18, WHITE, anchor="lm")
    save(img, "room_after.png")


def next_mark():
    img, d = canvas(1280, 300)
    title(d, "会話の文の最後に出る「▼」（文字が全部出たら点滅する）")
    d.polygon([(275, 70), (510, 70), (565, 120), (1105, 120), (1105, 280), (180, 280), (180, 120), (275, 70)], fill=DARK, outline=(60, 60, 60))
    text(d, (360, 96), "ココア", 26, WHITE, anchor="mm")
    s = "「……本についてですか？」"
    f = font(28, False)
    d.text((215, 190), s, font=f, fill=WHITE, anchor="lm")
    right = d.textbbox((215, 190), s, font=f, anchor="lm")[2]
    text(d, (right + 8, 193), "▼", 22, (25, 25, 25), anchor="lm")
    note(d, (right + 50, 180), "← 最後の文字のすぐ右", 18)
    save(img, "next_mark.png")


# ---------------- 資料画面（デザイナーさんへの依頼用の叩き台） ----------------

def tabs(d, selected="ココア", badges=("メリ", "その他")):
    x = 70
    for n in ["ココア", "メリ", "カネル", "レイジ", "その他", "アイテム"]:
        w = 100
        sel = n == selected
        box(d, (x, 40, x + w, 72), LIME, (205, 205, 205) if sel else DARK, 2, 8)
        text(d, (x + w / 2, 56), n, 17, WHITE, anchor="mm")
        if n in badges:
            d.ellipse((x + w - 10, 34, x + w + 4, 48), fill=RED)
        x += w + 10
    label(d, (1160, 56), "所持 40pt", 18, anchor="mm", pad=6)


def frame(d):
    box(d, (40, 26, 1240, 694), LIME, None, 3, 14)
    d.line([(60, 84), (1220, 84)], fill=LIME, width=2)


def info_row(d, rect, title_s, body=None, sub=None, new=False, arrow_right=False, fill=DARK, outline=LIME):
    x0, y0, x1, y1 = rect
    box(d, rect, outline, fill, 2, 8)
    tx = x0 + 12
    if new:
        label(d, (tx + 22, (y0 + y1) / 2 if not body else y0 + 18), "NEW", 13, (255, 255, 255), back=RED, line=RED, anchor="mm", pad=4)
        tx += 56
    text(d, (tx, y0 + 18 if body or sub else (y0 + y1) / 2), title_s, 16, WHITE, anchor="lm")
    if body:
        text(d, (x0 + 12, y0 + 32), body, 13, GRAY, bold=False)
    if sub:
        text(d, (x1 - 12, (y0 + y1) / 2), sub, 13, GRAY, bold=False, anchor="rm")
    if arrow_right:
        text(d, (x1 - 12, (y0 + y1) / 2), "▶", 14, LIME, anchor="rm")


def unlock_button(d, xy, s="30pt で解禁"):
    label(d, xy, s, 14, (255, 255, 255), back=(70, 70, 70), line=(70, 70, 70), anchor="mm", pad=6)


def profile_char():
    img, d = canvas()
    frame(d)
    tabs(d)
    placeholder(d, (70, 100, 250, 380), "立ち絵", 22)
    text(d, (270, 104), "基礎情報", 18, LIME)
    for i, s in enumerate(["名前　ココア", "年齢　17歳", "職業　高校生", "誕生日　10月27日", "身長　148cm", "体重　40kg", "特技　読書", "好き　文学小説", "嫌い　たけのこ"]):
        text(d, (270, 134 + i * 26), s, 15, WHITE, bold=False)
    text(d, (600, 104), "過去の情報　6件", 18, LIME)
    info_row(d, (600, 130, 1180, 160), "大切なもの：家族")
    info_row(d, (600, 168, 1180, 232), "現在", "最初のうちは真面目に図書館に通っていたが、授業に対して……")
    info_row(d, (600, 240, 1180, 300), "？ 幼少期", sub="70pt 必要（あと 30pt）", fill=(238, 238, 238), outline=GRAY)
    info_row(d, (600, 308, 1180, 368), "？ 交友関係", fill=(255, 255, 255), outline=LIME)
    d.rectangle((604, 312, 1176, 364), fill=(255, 255, 255))
    text(d, (890, 326), "？ 交友関係", 16, WHITE, anchor="mm")
    unlock_button(d, (890, 350))
    info_row(d, (600, 376, 1180, 436), "？ 事件の夜", sub="90pt 必要（あと 50pt）", fill=(238, 238, 238), outline=GRAY)
    d.rectangle((1196, 130, 1206, 436), fill=(215, 215, 215))
    d.rectangle((1196, 130, 1206, 230), fill=LIME)
    text(d, (70, 410), "手に入れた情報　12 / 20", 18, LIME)
    info_row(d, (70, 440, 560, 474), "図書館によく通っている", sub="2日目・対話", new=True, arrow_right=False)
    info_row(d, (70, 482, 560, 516), "メリとは顔見知り", sub="3日目・メリから聞き取り", new=True)
    info_row(d, (70, 524, 560, 558), "事件の夜は家にいなかった", sub="4日目・交渉")
    label(d, (150, 600), "すべて見る（12件）▶", 16, anchor="mm", pad=6)
    note(d, (600, 470), "解禁した枠: 見出しと文（長い文は2〜3行で「……」）", 16)
    note(d, (600, 498), "解禁前の枠: 足りるとき「◯pt で解禁」／足りないとき「◯pt 必要」", 16)
    note(d, (600, 526), "タブの右上の赤い丸: 新しい情報があるしるし", 16)
    save(img, "profile_char.png")


def popup(d, rect, title_s, lines, extra=None):
    x0, y0, x1, y1 = rect
    overlay_fill = (255, 255, 255)
    box(d, rect, LIME, overlay_fill, 3, 12)
    text(d, (x0 + 24, y0 + 22), title_s, 22, WHITE)
    text(d, (x1 - 24, y0 + 22), "×", 24, WHITE, anchor="ra")
    d.line([(x0 + 20, y0 + 62), (x1 - 20, y0 + 62)], fill=LIME, width=2)
    for i, s in enumerate(lines):
        text(d, (x0 + 24, y0 + 80 + i * 28), s, 17, WHITE if not s.startswith("・") else GRAY, bold=False)
    if extra:
        extra(d)


def dimmed_page(d):
    frame(d)
    tabs(d)
    d.rectangle((44, 90, 1236, 690), fill=(205, 205, 205))


def profile_detail():
    img, d = canvas()
    dimmed_page(d)
    popup(d, (250, 140, 1030, 560), "メリとは顔見知り", [
        "ココアは図書館でよくメリと顔を合わせている。メリが記事のために図書館の",
        "利用者に話を聞いて回っていたとき、ココアにも声をかけたのがきっかけだった。",
        "",
        "手に入れた日　　3日目",
        "手に入れ方　　　メリから聞き取り",
        "関係するキャラ　ココア・メリ",
        "手に入れた周回　今回（2周目）",
    ])
    label(d, (480, 172), "NEW", 13, (255, 255, 255), back=RED, line=RED, anchor="mm", pad=4)
    note(d, (250, 580), "手に入れた情報の行を押すと開く窓（過去の情報の全文もこの窓で見せる）", 18)
    save(img, "profile_detail.png")


def profile_unlock():
    img, d = canvas()
    dimmed_page(d)
    popup(d, (250, 160, 1030, 520), "交友関係", [
        "学校にはほとんど友人がいない。図書館で顔を合わせる大人たちとは、",
        "本の話をすることがある。最近は、図書館に取材に来ていた記者と話す",
        "ようになった。",
        "",
        "・30pt を使いました（残り 10pt）",
    ])
    text(d, (274, 152), "解禁しました", 16, LIME)
    note(d, (250, 540), "過去の情報を解禁した直後は、この窓で全文を広げて見せる", 18)
    save(img, "profile_unlock.png")


def profile_list():
    img, d = canvas()
    dimmed_page(d)
    x0, y0, x1, y1 = 240, 110, 1040, 640
    box(d, (x0, y0, x1, y1), LIME, (255, 255, 255), 3, 12)
    text(d, (x0 + 24, y0 + 20), "ココアについて手に入れた情報", 22, WHITE)
    text(d, (x0 + 420, y0 + 24), "12 / 20", 18, LIME)
    text(d, (x1 - 24, y0 + 20), "×", 24, WHITE, anchor="ra")
    for i, (s, sel) in enumerate([("すべて", True), ("今回の周回", False), ("NEW だけ", False)]):
        box(d, (x0 + 24 + i * 130, y0 + 66, x0 + 144 + i * 130, y0 + 96), LIME, (205, 205, 205) if sel else DARK, 2, 8)
        text(d, (x0 + 84 + i * 130, y0 + 81), s, 15, WHITE, anchor="mm")
    rows = [("図書館によく通っている", "2日目・対話", True), ("メリとは顔見知り", "3日目・メリから聞き取り", True),
            ("事件の夜は家にいなかった", "4日目・交渉", False), ("両親には不登校を隠している", "5日目・対話", False),
            ("キノコの図鑑を持ち歩いている", "1周目・対話", False), ("カネルを怖がっている", "1周目・聞き取り", False)]
    for i, (s, sub, new) in enumerate(rows):
        y = y0 + 112 + i * 52
        info_row(d, (x0 + 24, y, x1 - 40, y + 44), s, sub=sub, new=new)
    info_row(d, (x0 + 24, y0 + 424, x1 - 40, y0 + 468), "？？？（まだ手に入れていない情報 × 8）", fill=(238, 238, 238), outline=GRAY)
    d.rectangle((x1 - 30, y0 + 112, x1 - 22, y0 + 470), fill=(215, 215, 215))
    d.rectangle((x1 - 30, y0 + 112, x1 - 22, y0 + 220), fill=LIME)
    save(img, "profile_list.png")


def profile_other():
    img, d = canvas()
    frame(d)
    tabs(d, selected="その他", badges=())
    text(d, (70, 104), "キャラに関係しない情報　4 / 14", 18, LIME)
    for i, (s, sel) in enumerate([("全て", True), ("ポイント解禁", False), ("会話解禁", False)]):
        box(d, (70 + i * 130, 136, 190 + i * 130, 166), LIME, (205, 205, 205) if sel else DARK, 2, 8)
        text(d, (130 + i * 130, 151), s, 15, WHITE, anchor="mm")
    rows = [("事件当日は雨だった", None, True, "sel"), ("現場は旧校舎の3階", None, True, ""), ("旧校舎の見取り図", None, False, ""),
            ("？ 停電の時刻", "30pt で解禁", False, "ok"), ("？ 旧校舎の鍵の持ち主", "60pt 必要（あと 20pt）", False, "ng"),
            ("？？？", None, False, "unk")]
    for i, (s, sub, new, kind) in enumerate(rows):
        y = 180 + i * 54
        fill = (205, 205, 205) if kind == "sel" else (238, 238, 238) if kind in ("ng", "unk") else DARK
        outline = GRAY if kind in ("ng", "unk") else LIME
        info_row(d, (70, y, 560, y + 46), s, new=new, fill=fill, outline=outline, sub=sub if kind == "ng" else None)
        if kind == "ok":
            unlock_button(d, (490, y + 23))
    box(d, (590, 104, 1210, 670), LIME, None, 2, 10)
    text(d, (610, 124), "事件当日は雨だった", 22, WHITE)
    label(d, (900, 136), "NEW", 13, (255, 255, 255), back=RED, line=RED, anchor="mm", pad=4)
    for i, s in enumerate(["事件が起きた日は、朝から夕方まで強い雨が降っていた。", "夜には雨はやんでいたが、地面はぬかるんでいた。"]):
        text(d, (610, 174 + i * 28), s, 16, WHITE, bold=False)
    for i, s in enumerate(["手に入れた日　　1日目", "手に入れ方　　　スマホ（マキナ）", "関係するキャラ　なし", "手に入れた周回　今回（2周目）"]):
        text(d, (610, 520 + i * 30), s, 16, GRAY, bold=False)
    note(d, (70, 520), "左に一覧、右に選んだ情報の詳しい表示", 16)
    save(img, "profile_other.png")


def profile_item():
    img, d = canvas()
    frame(d)
    tabs(d, selected="アイテム", badges=())
    text(d, (70, 104), "キーアイテム　5 / 12", 18, LIME)
    names = ["現場の鍵", "破れたメモ", "貸出カード", "古い写真", "濡れた傘"]
    for i in range(12):
        r, c = divmod(i, 4)
        x, y = 70 + c * 120, 140 + r * 150
        box(d, (x, y, x + 100, y + 110), LIME if i < 5 else GRAY, (205, 205, 205) if i == 0 else DARK, 2, 8)
        if i < 5:
            placeholder(d, (x + 15, y + 12, x + 85, y + 82), "絵", 14)
            text(d, (x + 50, y + 96), names[i], 12, WHITE, anchor="mm")
        else:
            text(d, (x + 50, y + 55), "？", 30, GRAY, anchor="mm")
        if i in (0, 1):
            label(d, (x + 82, y + 6), "NEW", 11, (255, 255, 255), back=RED, line=RED, anchor="mm", pad=3)
    box(d, (590, 104, 1210, 670), LIME, None, 2, 10)
    text(d, (610, 124), "現場の鍵", 22, WHITE)
    for i, s in enumerate(["旧校舎の3階の教室の鍵。職員室にあるはずのものが、", "なぜかココアのかばんから見つかった。"]):
        text(d, (610, 174 + i * 28), s, 16, WHITE, bold=False)
    placeholder(d, (610, 250, 790, 420), "アイテムの大きい絵", 15)
    for i, s in enumerate(["手に入れた日　　3日目", "手に入れ方　　　ココアに交渉", "関係するキャラ　ココア", "手に入れた周回　今回（2周目）"]):
        text(d, (610, 520 + i * 30), s, 16, GRAY, bold=False)
    note(d, (70, 610), "左にマス目、右に選んだアイテムの詳しい表示（見るだけ）", 16)
    save(img, "profile_item.png")


def profile_now():
    img, d = canvas()
    title(d, "今の資料画面（形だけ）")
    box(d, (60, 70, 1220, 690), (60, 60, 60), DARK, 4, 16)
    text(d, (100, 110), "キャラの名前", 26, WHITE)
    placeholder(d, (100, 160, 290, 400), "立ち絵", 20)
    box(d, (320, 180, 500, 340), WHITE, (230, 230, 230), 2)
    text(d, (410, 260), "？", 30, WHITE, anchor="mm")
    text(d, (780, 120), "作業方法", 40, LIME, anchor="mm")
    for i, n in enumerate(["ココア", "メリ", "カネル", "レイジ"]):
        r, c = divmod(i, 2)
        box(d, (600 + c * 190, 170 + r * 70, 770 + c * 190, 220 + r * 70), (60, 60, 60), DARK, 2)
        text(d, (685 + c * 190, 195 + r * 70), n, 20, WHITE, anchor="mm")
    box(d, (100, 440, 500, 640), WHITE, (230, 230, 230), 2)
    text(d, (300, 540), "？", 40, WHITE, anchor="mm")
    box(d, (600, 330, 800, 640), (60, 60, 60), None, 3, 30)
    box(d, (840, 330, 1040, 640), (60, 60, 60), None, 3, 30)
    note(d, (560, 660), "文を入れられる枠が2〜4つしかない", 18)
    save(img, "profile_now.png")


def nine_slice():
    img, d = canvas(1000, 380)
    text(d, (30, 20), "作る画像（1つ）", 20, LIME)
    x0, y0, x1, y1 = 60, 80, 240, 200
    box(d, (x0, y0, x1, y1), WHITE, None, 4, 18)
    for x in (x0 + 40, x1 - 40):
        d.line([(x, y0 - 10), (x, y1 + 10)], fill=RED, width=2)
    for y in (y0 + 40, y1 - 40):
        d.line([(x0 - 10, y), (x1 + 10, y)], fill=RED, width=2)
    for i, s in enumerate(["赤い線で9つに分ける", "・四隅（角）は伸ばさない", "・辺と真ん中だけ伸ばす"]):
        text(d, (40, 230 + i * 30), s, 17, WHITE if i == 0 else GRAY, bold=i == 0)
    text(d, (380, 20), "ゲームの中で、文字の長さに合わせて伸ばす", 20, LIME)
    for i, (s, w) in enumerate([("その他", 120), ("アイテム", 150), ("ポイント解禁", 200), ("手に入れた情報の一覧の行", 560)]):
        y = 70 + i * 62
        box(d, (400, y, 400 + w, y + 44), WHITE, None, 3, 12)
        text(d, (400 + w / 2, y + 22), s, 18, WHITE, anchor="mm")
    text(d, (400, 330), "文字はゲーム側のフォント。角の丸みは、どの長さでも同じ", 16, GRAY, bold=False)
    save(img, "nine_slice.png")


def tint_explain():
    img, d = canvas(1100, 520)
    text(d, (30, 20), "1枚の外枠（白か明るい灰色）に、ゲーム側で色を付けて状態を出す（図の色は例）", 22, LIME)
    states = [("ふつう", (60, 60, 60), (255, 255, 255)), ("選ばれている", (25, 25, 25), (205, 205, 205)),
              ("足りる", (60, 60, 60), (240, 240, 240)), ("足りない", (160, 160, 160), (238, 238, 238)), ("まだ（？？？）", (190, 190, 190), (246, 246, 246))]
    for i, (s, line, fill) in enumerate(states):
        y = 80 + i * 70
        box(d, (40, y, 440, y + 50), line, fill, 3, 10)
        text(d, (60, y + 25), s, 20, WHITE, anchor="lm")
    text(d, (500, 90), "できること", 20, LIME)
    for i, s in enumerate(["・線と中の塗りを、同じ色の明るい・暗いで変える", "・全体を暗くする（足りない・まだ）"]):
        text(d, (500, 130 + i * 32), s, 17, WHITE, bold=False)
    text(d, (500, 230), "1枚ではできないこと", 20, RED)
    for i, s in enumerate(["・線と中の塗りを、まったく別の色にする（例: 線は緑、中は赤）", "　→ 線の画像と塗りの画像の2枚に分ければできる"]):
        text(d, (500, 270 + i * 32), s, 17, WHITE, bold=False)
    save(img, "tint_explain.png")


if __name__ == "__main__":
    monitor_before()
    monitor_after()
    monitor_hover_simple()
    monitor_final()
    morning_flow()
    room_points()
    room_after()
    next_mark()
    profile_now()
    profile_char()
    profile_detail()
    profile_unlock()
    profile_list()
    profile_other()
    profile_item()
    nine_slice()
    tint_explain()
