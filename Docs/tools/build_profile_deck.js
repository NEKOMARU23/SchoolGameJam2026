// 資料画面のデザイン相談（Docs/資料画面のデザイン相談.md）をパワポにする
// 使い方: pptxgenjs@3.12.0 と image-size@1.1.1 を npm install したフォルダで
//   node build_profile_deck.js 出力.pptx
// 図は Docs/images/wire/ の四角と文字だけの図（先に python Docs/tools/make_wireframes.py で作る）
// 最後にテーマの色を書き込む applyTheme は、Claude Code の pptx スキルのもの（ない場合はその行を消しても作れる）
const path = require("path");
const pptxgen = require("pptxgenjs");
const sizeOf = require("image-size");
const { applyTheme } = require("C:/Users/nekos/.claude/skills/synced/05ca163d-714f-43ed-9925-75522b8aa530_6d877ae3-8035-44c0-8104-bec50cefe451/pptx/scripts/apply_theme.js");

const IMG = "C:/Unity/SchoolGameJam2026/Docs/images/wire/";   // 四角と文字だけの図（Docs/tools/make_wireframes.py で作る）
const OUT = process.argv[2] || "資料画面のデザイン相談.pptx";

// 色はゲームの見た目に似せず、中立の色にする（白い背景・黒い文字・灰色。強調は濃い青）
const THEME = {
  name: "SchoolGameJam Neutral",
  headFontFace: "Yu Gothic",
  bodyFontFace: "Yu Gothic",
  colors: {
    dk1: "1F2328", lt1: "FFFFFF", dk2: "57606A", lt2: "F3F4F6",
    accent1: "1F4E8C", accent2: "C83232", accent3: "4A7FC1", accent4: "8C959F", accent5: "D0D7DE", accent6: "9A6700",
    hlink: "1F4E8C", folHlink: "4A7FC1",
  },
};

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.33 x 7.5
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = "資料画面のデザイン相談";
const C = pres.SchemeColor;

const W = 13.333, H = 7.5, M = 0.6;

// すべての文字を日本語（ja-JP）にして、日本語の改行ルール（行頭の「。」を避けるなど）を効かせる
const addSlideOriginal = pres.addSlide.bind(pres);
pres.addSlide = (opts) => {
  const slide = addSlideOriginal(opts);
  const addTextOriginal = slide.addText.bind(slide);
  slide.addText = (text, options = {}) => {
    // 見出し（placeholder）は、指定を足すと太字などの設定が消えるので、そのままにする
    if (options.placeholder) return addTextOriginal(text, options);
    const withLang = Array.isArray(text)
      ? text.map((r) => ({ ...r, options: { lang: "ja-JP", ...(r.options || {}) } }))
      : text;
    return addTextOriginal(withLang, { lang: "ja-JP", ...options });
  };
  return slide;
};

pres.defineSlideMaster({
  title: "TITLE",
  background: { color: C.background1 },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: M, y: 2.4, w: W - 2 * M, h: 1.2, fontSize: 44, bold: true, color: C.accent1, valign: "bottom", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: M, y: 3.8, w: W - 2 * M, h: 1.6, fontSize: 18, color: C.text1, valign: "top", margin: 0 }, text: "" } },
  ],
});

pres.defineSlideMaster({
  title: "SECTION",
  background: { color: C.background1 },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: M, y: 2.9, w: W - 2 * M, h: 1.0, fontSize: 40, bold: true, color: C.accent1, valign: "middle", margin: 0 }, text: "" } },
    { placeholder: { options: { name: "body", type: "body", x: M, y: 4.0, w: W - 2 * M, h: 0.8, fontSize: 18, color: C.text2, valign: "top", margin: 0 }, text: "" } },
  ],
  slideNumber: { x: W - 1.0, y: H - 0.5, w: 0.5, h: 0.3, fontSize: 11, color: C.text2, align: "right" },
});

pres.defineSlideMaster({
  title: "CONTENT",
  background: { color: C.background1 },
  objects: [
    { placeholder: { options: { name: "title", type: "title", x: M, y: 0.35, w: W - 2 * M, h: 0.8, fontSize: 30, bold: true, color: C.text1, valign: "middle", margin: 0 }, text: "" } },
  ],
  slideNumber: { x: W - 1.0, y: H - 0.5, w: 0.5, h: 0.3, fontSize: 11, color: C.text2, align: "right" },
});

// --- 補助 ---
function fit(file, x, y, w, h, align = "center") {
  const s = sizeOf(path.join(IMG, file));
  const r = Math.min(w / s.width, h / s.height);
  const iw = s.width * r, ih = s.height * r;
  const ix = align === "left" ? x : x + (w - iw) / 2;
  return { path: path.join(IMG, file), x: ix, y: y + (h - ih) / 2, w: iw, h: ih };
}
function image(slide, file, x, y, w, h, name, align) {
  const o = fit(file, x, y, w, h, align);
  slide.addImage({ ...o, objectName: name, altText: name });
  return o;
}
function bullets(slide, items, x, y, w, h, size = 16, name = "本文") {
  const runs = items.map((it, i) => {
    const isSub = typeof it === "object" && it.sub;
    const text = typeof it === "object" ? it.text : it;
    return {
      text,
      options: {
        bullet: isSub ? { indent: 18 } : { indent: 16 },
        indentLevel: isSub ? 1 : 0,
        fontSize: isSub ? size - 2 : size,
        color: isSub ? C.text2 : C.text1,
        paraSpaceAfter: 6,
        breakLine: i < items.length - 1,
      },
    };
  });
  slide.addText(runs, { x, y, w, h, valign: "top", margin: 0, isTextBox: true, objectName: name });
}
function caption(slide, text, x, y, w, name = "説明") {
  slide.addText(text, { x, y, w, h: 0.4, fontSize: 12, color: C.text2, margin: 0, isTextBox: true, objectName: name });
}
function card(slide, x, y, w, h, name) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.08, fill: { color: C.background2 }, line: { color: C.accent5, width: 1 }, objectName: name });
}
function badge(slide, n, x, y, name) {
  slide.addShape(pres.shapes.OVAL, { x, y, w: 0.42, h: 0.42, fill: { color: C.accent1 }, line: { type: "none" }, objectName: name + "の丸" });
  slide.addText(String(n), { x, y, w: 0.42, h: 0.42, fontSize: 16, bold: true, color: C.background1, align: "center", valign: "middle", margin: 0, isTextBox: true, objectName: name + "の番号" });
}
function content(title, section) {
  const s = pres.addSlide({ masterName: "CONTENT", sectionTitle: section });
  s.addText(title, { placeholder: "title" });
  return s;
}

// ===== 表紙 =====
pres.addSection({ title: "表紙" });
{
  const s = pres.addSlide({ masterName: "TITLE", sectionTitle: "表紙" });
  s.addText("資料画面のデザイン相談", { placeholder: "title" });
  s.addText([
    { text: "キャラの切り替えを画面の上のタブにした、新しいレイアウトのお願い", options: { breakLine: true } },
    { text: "図は四角と文字だけで描いた叩き台です（絵の場所は灰色の四角）。色・形・配置は自由に変えてください", options: { fontSize: 14, color: C.text2 } },
  ], { placeholder: "body" });
  image(s, "profile_char.png", 8.3, 4.9, 4.4, 2.3, "叩き台の画像");
}

// ===== 1. お願いしたいこと =====
pres.addSection({ title: "お願いしたいこと" });
{
  const s = content("お願いしたいこと", "お願いしたいこと");
  s.addText("資料画面を、新しいレイアウトで描き直してほしいです。描いてほしい画面は5つです", { x: M, y: 1.35, w: 12, h: 0.5, fontSize: 18, color: C.text1, margin: 0, isTextBox: true, objectName: "リード" });
  const screens = ["キャラのページ（ココア・メリ・カネル・レイジ）", "「手に入れた情報」の詳しい表示（窓）", "「手に入れた情報」の一覧（窓）", "「その他」のページ", "「アイテム」のページ"];
  screens.forEach((t, i) => {
    const y = 2.15 + i * 0.72;
    badge(s, i + 1, M, y, `画面${i + 1}`);
    s.addText(t, { x: M + 0.6, y, w: 5.6, h: 0.42, fontSize: 17, color: C.text1, valign: "middle", margin: 0, isTextBox: true, objectName: `画面${i + 1}の名前` });
  });
  card(s, 7.1, 2.15, 5.6, 1.15, "素材のカード");
  s.addText([
    { text: "作ってほしい素材", options: { bold: true, color: C.accent1, fontSize: 18, breakLine: true } },
    { text: "素材の分け方と一覧は「素材」の章にまとめました", options: { fontSize: 15, color: C.text1 } },
  ], { x: 7.35, y: 2.3, w: 5.1, h: 0.9, valign: "top", margin: 0, isTextBox: true, objectName: "素材の文" });
  card(s, 7.1, 3.6, 5.6, 1.15, "決めてほしいことのカード");
  s.addText([
    { text: "決めてほしいこと", options: { bold: true, color: C.accent1, fontSize: 18, breakLine: true } },
    { text: "配置・フォント・状態ごとの色などは、最後のスライドにまとめました", options: { fontSize: 15, color: C.text1 } },
  ], { x: 7.35, y: 3.75, w: 5.1, h: 0.9, valign: "top", margin: 0, isTextBox: true, objectName: "決めてほしいことの文" });
}

// ===== 2. なぜ変えたいか =====
{
  const s = content("なぜ変えたいか", "お願いしたいこと");
  image(s, "profile_now.png", M, 1.5, 7.0, 3.94, "今の資料画面", "left");
  caption(s, "今の資料画面", M, 5.55, 7.0);
  bullets(s, [
    "載せたい情報の量に対して、場所が足りない。今は文を入れられる枠が2〜4つしかない",
    "「手に入れた情報」は2周目以降も引き継ぐ予定なので、遊ぶほど数が増えていく",
    "「その他」「アイテム」のページを置く場所がない",
    "見出しの「作業方法」は、連絡の行き違いで入った文。新しいレイアウトでは、この見出しはなくなる",
  ], 8.0, 1.55, 4.7, 5.0, 16);
}

// ===== 3. 1キャラ分の資料に載せる情報 =====
{
  const s = content("1キャラ分の資料に載せる情報", "お願いしたいこと");
  const head = (t) => ({ text: t, options: { bold: true, color: C.background1, fill: { color: C.accent1 }, fontSize: 15 } });
  const cell = (t, bold) => ({ text: t, options: { color: C.text1, fill: { color: C.background2 }, fontSize: 14, bold: !!bold } });
  s.addTable([
    [head("種類"), head("中身"), head("量")],
    [cell("基礎情報", true), cell("名前・年齢・職業・誕生日・身長・体重・特技・好きなもの・嫌いなもの"), cell("9項目（どれも短い）。ポイントで解禁する")],
    [cell("過去の情報", true), cell("ココアの例: 「大切なもの：家族」、幼少期（4行くらいの文）、現在（5行くらいの文）"), cell("3項目以上（これから増やす予定。長い文もある）。1つずつポイントで解禁する")],
    [cell("手に入れた情報", true), cell("会話で知った情報（例:「メリのアリバイ」）"), cell("遊ぶほど増える（2周目以降も引き継ぐ）")],
  ], { x: M, y: 1.45, w: W - 2 * M, colW: [2.1, 5.6, 4.43], border: { type: "solid", color: THEME.colors.dk1, pt: 2 }, margin: 0.1, rowH: [0.45, 0.8, 0.8, 0.6], objectName: "載せる情報の表" });
  s.addText("キャラごとのページとは別に、次の2つのページもあります", { x: M, y: 4.5, w: 12, h: 0.4, fontSize: 16, color: C.text1, margin: 0, isTextBox: true, objectName: "別のページの説明" });
  [["その他", "キャラに関係しない情報（例:「事件当日は雨だった」）"], ["アイテム", "キーアイテム（例:「現場の鍵」）"]].forEach(([t, d], i) => {
    const x = M + i * 6.2;
    card(s, x, 5.1, 5.9, 1.3, t + "のカード");
    s.addText([
      { text: t, options: { bold: true, fontSize: 18, color: C.accent1, breakLine: true } },
      { text: d, options: { fontSize: 15, color: C.text1 } },
    ], { x: x + 0.25, y: 5.25, w: 5.4, h: 1.0, valign: "top", margin: 0, isTextBox: true, objectName: t + "の文" });
  });
}

// ===== 4. 描いてほしい画面 =====
pres.addSection({ title: "描いてほしい画面" });
{
  const s = pres.addSlide({ masterName: "SECTION", sectionTitle: "描いてほしい画面" });
  s.addText("描いてほしい画面", { placeholder: "title" });
  s.addText("図の文はココアの実際のデータです。「手に入れた情報」「その他」「アイテム」の文は、見た目を確かめるための作り話の例です", { placeholder: "body" });
}
{
  const s = content("① キャラのページ", "描いてほしい画面");
  image(s, "profile_char.png", M, 1.4, 6.6, 3.71, "キャラのページの叩き台", "left");
  caption(s, "ココア・メリ・カネル・レイジで同じ形", M, 5.25, 6.6);
  bullets(s, [
    "上のタブ: ココア・メリ・カネル・レイジ・その他・アイテム。右上に赤い丸の印が付くことがある（新しい情報があるしるし）",
    "所持ポイント（右上。例:「所持 40pt」）",
    "立ち絵と基礎情報（左上）",
    { text: "解禁する前は名前と立ち絵だけ。残りの8項目は「？ 基礎情報」の枠1つにまとめて隠れる", sub: true },
    "過去の情報（右側）。見出しの横に件数（例:「6件」）",
    { text: "解禁した枠: 見出しと文。長い文は2〜3行で切って「……」", sub: true },
    { text: "解禁前の枠: 「？ 見出し」と、足りるとき「◯pt で解禁」、足りないとき「◯pt 必要（あと◯pt）」", sub: true },
    { text: "多いときは右の欄の中でスクロール", sub: true },
    "手に入れた情報（左下）: 「12 / 20」、新しい3件、NEW の印、行の右端に「▶」、「すべて見る（12件）▶」",
  ], 7.5, 1.4, 5.25, 5.7, 15);
}
{
  const s = content("② 「手に入れた情報」の詳しい表示（窓）", "描いてほしい画面");
  s.addText("手に入れた情報の行を押すと開く窓です。過去の情報の枠を押したとき・解禁した直後も、同じ窓で全文を見せます", { x: M, y: 1.3, w: 12.1, h: 0.6, fontSize: 16, color: C.text1, margin: 0, isTextBox: true, objectName: "リード" });
  image(s, "profile_detail.png", M, 2.05, 5.9, 3.32, "詳しい表示の叩き台", "left");
  image(s, "profile_unlock.png", 6.83, 2.05, 5.9, 3.32, "解禁した直後の叩き台", "left");
  bullets(s, ["手に入れた情報: 見出し、NEW の印、説明の文、手に入れた日・手に入れ方・関係するキャラ・周回", "右上に「×」（閉じる）"], M, 5.55, 5.9, 1.4, 14, "左の説明");
  bullets(s, ["過去の情報を解禁した直後: 「解禁しました」、見出し、文、使ったポイント", "右上に「×」（閉じる）"], 6.83, 5.55, 5.9, 1.4, 14, "右の説明");
}
{
  const s = content("③ 「手に入れた情報」の一覧（窓）", "描いてほしい画面");
  image(s, "profile_list.png", M, 1.4, 7.4, 4.16, "一覧の叩き台", "left");
  caption(s, "「すべて見る」を押すと開く窓", M, 5.7, 7.4);
  bullets(s, [
    "見出し（例:「ココアについて手に入れた情報」）と「12 / 20」",
    "小タブ: 「すべて」「今回の周回」「NEW だけ」",
    "一覧の行（NEW の印、見出し、手に入れた日と手に入れ方）",
    "まだ手に入れていない情報は「？？？」（数だけ。例:「？？？（まだ手に入れていない情報 × 8）」）",
    "数が多いときはスクロールする",
  ], 8.3, 1.4, 4.45, 5.6, 15);
}
{
  const s = content("④ 「その他」のページ", "描いてほしい画面");
  image(s, "profile_other.png", M, 1.4, 7.4, 4.16, "その他のページの叩き台", "left");
  caption(s, "左に一覧、右に選んだ情報の詳しい表示", M, 5.7, 7.4);
  bullets(s, [
    "一覧の上に「キャラに関係しない情報」と「4 / 14」、小タブ「全て」「ポイント解禁」「会話解禁」",
    "一覧の行は3種類",
    { text: "手に入れた情報（NEW の印が付くことがある。選んでいる行は色が変わる）", sub: true },
    { text: "ポイントで解禁できる情報（足りるとき「◯pt で解禁」／足りないとき「◯pt 必要（あと◯pt）」）", sub: true },
    { text: "会話で手に入る、まだの情報（「？？？」）", sub: true },
    "右の詳しい表示: 見出し、説明の文、手に入れた日・手に入れ方・関係するキャラ・周回",
  ], 8.3, 1.4, 4.45, 5.6, 14);
}
{
  const s = content("⑤ 「アイテム」のページ", "描いてほしい画面");
  image(s, "profile_item.png", M, 1.4, 7.4, 4.16, "アイテムのページの叩き台", "left");
  caption(s, "左にマス目、右に選んだアイテムの詳しい表示", M, 5.7, 7.4);
  bullets(s, [
    "マス目: アイテムの絵と名前",
    { text: "まだ持っていないものは「？」", sub: true },
    { text: "NEW の印が付くことがある。選んでいるマスは色が変わる", sub: true },
    "右の詳しい表示: 名前、説明の文、大きい絵、手に入れた日・手に入れ方・関係するキャラ・周回",
    "アイテムは見るだけ（「使う」操作はない）",
  ], 8.3, 1.4, 4.45, 5.6, 15);
}

// ===== 5. 作ってほしい素材 =====
pres.addSection({ title: "作ってほしい素材" });
{
  const s = pres.addSlide({ masterName: "SECTION", sectionTitle: "作ってほしい素材" });
  s.addText("作ってほしい素材", { placeholder: "title" });
  s.addText("素材の形は、ページの図を参考にしてください。自由に変えてください", { placeholder: "body" });
}
{
  const s = content("素材の分け方", "作ってほしい素材");
  const rules = [
    ["背景はページごとに1枚", "外枠・区切り線・立ち絵の枠など、動かない部分をすべて入れる"],
    ["文字は描き込まない", "ボタンや見出しの文、数字（「4 / 14」「30pt」など）は、ゲームの中でフォントで出す"],
    ["文字が入る土台は、伸び縮みできる外枠に", "四隅はそのまま、辺と真ん中だけを伸ばして使う（9スライス）"],
    ["状態の違いはゲーム側で色を変える", "差分の絵は描かなくてよい。外枠は1種類だけ、白か明るい灰色で"],
    ["別の素材にするのは", "上に重ねる印（タブの赤い丸、NEW など）と、アイテムの絵"],
  ];
  rules.forEach(([t, d], i) => {
    const y = 1.45 + i * 1.08;
    badge(s, i + 1, M, y + 0.05, `ルール${i + 1}`);
    s.addText([
      { text: t, options: { bold: true, fontSize: 17, color: C.accent1, breakLine: true } },
      { text: d, options: { fontSize: 14, color: C.text1 } },
    ], { x: M + 0.6, y, w: 5.9, h: 0.95, valign: "top", margin: 0, isTextBox: true, objectName: `ルール${i + 1}の文` });
  });
  image(s, "nine_slice.png", 7.3, 1.6, 5.4, 2.0, "9スライスの説明");
  caption(s, "伸び縮みできる外枠（9スライス）: 角の丸みや飾りは四隅に収め、辺と真ん中は伸ばしても崩れない模様に", 7.3, 3.75, 5.4);
  card(s, 7.3, 4.6, 5.4, 1.55, "今ある素材のカード");
  s.addText([
    { text: "今ある素材を使うもの（新しく作らなくてよい）", options: { bold: true, fontSize: 15, color: C.accent1, breakLine: true } },
    { text: "戻るボタン（今の緑の矢印）", options: { bullet: { indent: 14 }, fontSize: 14, color: C.text1, breakLine: true } },
    { text: "上のタブのキャラ4人（今ある名前プレートの画像）", options: { bullet: { indent: 14 }, fontSize: 14, color: C.text1, breakLine: true } },
    { text: "キャラの立ち絵（表情の差分も）", options: { bullet: { indent: 14 }, fontSize: 14, color: C.text1 } },
  ], { x: 7.5, y: 4.72, w: 5.0, h: 1.7, valign: "top", margin: 0, isTextBox: true, objectName: "今ある素材の文" });
}
{
  const s = content("背景（ページごとに1枚）", "作ってほしい素材");
  s.addText("文字や、上に重ねる部品（タブ・行・ボタンなど）は背景には入れません", { x: M, y: 1.25, w: 12, h: 0.4, fontSize: 15, color: C.text2, margin: 0, isTextBox: true, objectName: "リード" });
  const bgs = [
    ["キャラのページ", "外枠、タブの下の区切り線、立ち絵の枠"],
    ["その他", "外枠、区切り線、右の詳しい表示の枠"],
    ["アイテム", "外枠、区切り線、右の詳しい表示の枠、大きい絵を置く枠"],
    ["窓（②③で共通）", "窓の枠、見出しの下の線。中身の量で大きさが変わるので、伸び縮みできる外枠に"],
  ];
  bgs.forEach(([t, d], i) => {
    const x = M + i * 3.07;
    card(s, x, 1.9, 2.85, 1.75, t + "のカード");
    s.addText([
      { text: t, options: { bold: true, fontSize: 17, color: C.accent1, breakLine: true } },
      { text: d, options: { fontSize: 14, color: C.text1 } },
    ], { x: x + 0.2, y: 2.05, w: 2.45, h: 1.5, valign: "top", margin: 0, isTextBox: true, objectName: t + "の文" });
  });
  s.addText("形は「描いてほしい画面」の各ページの図を参考にしてください", { x: M, y: 3.95, w: 12, h: 0.4, fontSize: 15, color: C.text2, margin: 0, isTextBox: true, objectName: "補足" });
}
{
  const s = content("外枠（すべて伸び縮みできる外枠）", "作ってほしい素材");
  s.addText("文字が入る土台は、ページをまたいで同じ外枠を使い回します。各外枠は1種類だけ作ってください。色の違いは、ゲーム側で色を変えて出します", { x: M, y: 1.25, w: 12.1, h: 0.65, fontSize: 15, color: C.text2, margin: 0, isTextBox: true, objectName: "リード" });
  const cols = [
    ["① 角の丸い外枠", "上のタブ（その他・アイテム）、小タブ、「すべて見る」ボタン、所持ポイントの枠"],
    ["② 解禁ボタン", "「◯pt で解禁」ボタン。枠ではなく、中まで塗りつぶしたボタン"],
    ["③ 情報の枠", "過去の情報、基礎情報の「？」、手に入れた情報の行、一覧の行、その他の一覧の行。高さは中身で変わる"],
  ];
  cols.forEach(([t, d], i) => {
    const y = 2.05 + i * 1.35, w = 5.6;
    card(s, M, y, w, 1.2, t + "のカード");
    s.addText([
      { text: t, options: { bold: true, fontSize: 17, color: C.accent1, breakLine: true } },
      { text: d, options: { fontSize: 13, color: C.text1 } },
    ], { x: M + 0.2, y: y + 0.12, w: w - 0.4, h: 1.0, valign: "top", margin: 0, isTextBox: true, objectName: t + "の文" });
  });
  image(s, "tint_explain.png", 6.55, 2.05, 6.2, 3.9, "1枚の外枠に色を付けて状態を出す図");
}
{
  const s = content("印・小物と、アイテムのページだけで使うもの", "作ってほしい素材");
  const items = [
    ["タブの赤い丸", "タブの右上に重ねる"],
    ["NEW の印", "一覧の行やアイテムのマスに重ねる"],
    ["スクロールバー", "過去の情報の欄・一覧。溝とつまみの2つ"],
    ["アイテムのマス", "大きさは固定。1種類だけ。「選ばれている」「持っていない」はゲーム側で色を変える"],
    ["アイテムの絵", "アイテムの数だけ。マス用の小さい絵と、詳しい表示用の大きい絵（同じ絵を縮めてもよい）。一覧は決まり次第お伝えします"],
  ];
  items.forEach(([t, d], i) => {
    const x = M + i * 2.46, w = 2.25;
    card(s, x, 1.45, w, 2.55, t + "のカード");
    s.addText([
      { text: t, options: { bold: true, fontSize: 16, color: C.accent1, breakLine: true } },
      { text: d, options: { fontSize: 14, color: C.text1 } },
    ], { x: x + 0.15, y: 1.6, w: w - 0.3, h: 2.3, valign: "top", margin: 0, isTextBox: true, objectName: t + "の文" });
  });
  s.addText("置く場所は「描いてほしい画面」の各ページの図を見てください", { x: M, y: 4.3, w: 12, h: 0.4, fontSize: 15, color: C.text2, margin: 0, isTextBox: true, objectName: "補足" });
}

// ===== 6. 決めてほしいこと =====
pres.addSection({ title: "決めてほしいこと" });
{
  const s = content("決めてほしいこと・確認したいこと", "決めてほしいこと");
  const asks = [
    ["描いてほしい5つの画面の、細かい配置と見た目", ""],
    ["使いたいフォント", ""],
    ["状態ごとの色（外枠・中の塗り・文字）", "ふつう／選ばれている（タブ、その他の一覧の行、アイテムのマス）・足りる／足りない（解禁前の枠）・まだ（「？？？」の行、持っていないアイテム）"],
    ["タブの見た目のそろえ方", "キャラ4人のタブは今ある名前プレート（文字が画像に描き込まれている）、その他・アイテムのタブはフォントの文字なので、見た目がそろわないかもしれない（例: 名前プレートに近いフォントを使う、キャラ4人もフォントにする）"],
    ["素材を作ってもらえるか", "特にアイテムの絵（数はアイテムの一覧が決まり次第お伝えします）"],
  ];
  asks.forEach(([t, d], i) => {
    const y = 1.4 + i * 1.0;
    badge(s, i + 1, M, y + 0.03, `確認${i + 1}`);
    s.addText(d ? [
      { text: t, options: { bold: true, fontSize: 17, color: C.text1, breakLine: true } },
      { text: d, options: { fontSize: 13, color: C.text2 } },
    ] : [{ text: t, options: { bold: true, fontSize: 17, color: C.text1 } }], { x: M + 0.6, y, w: 11.5, h: 0.95, valign: "top", margin: 0, isTextBox: true, objectName: `確認${i + 1}の文` });
  });
  s.addText("わからないことがあれば、いつでも聞いてください", { x: M, y: 6.55, w: 12, h: 0.4, fontSize: 16, bold: true, color: C.accent1, margin: 0, isTextBox: true, objectName: "結び" });
}

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  console.log("written", OUT);
})();
