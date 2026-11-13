from pathlib import Path
import re
from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'docs/SRS_HeThongThuePhongTro.md'
OUT = Path(__file__).parent / 'SRS_HeThongThuePhongTro_v1.0.docx'
doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Cm(21), Cm(29.7)
sec.top_margin, sec.bottom_margin = Cm(1.8), Cm(1.8)
sec.left_margin, sec.right_margin = Cm(2), Cm(2)
sec.header_distance, sec.footer_distance = Cm(.8), Cm(.8)
normal = doc.styles['Normal']
normal.font.name = 'Times New Roman'
normal.font.size = Pt(12)
normal.font.color.rgb = RGBColor(0, 0, 0)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.1
normal.paragraph_format.widow_control = True
for name, size in [('Title', 25), ('Subtitle', 16), ('Heading 1', 17), ('Heading 2', 14), ('Heading 3', 12.5)]:
    style = doc.styles[name]
    style.font.name = 'Times New Roman'
    style.font.size = Pt(size)
    style.font.color.rgb = RGBColor(0, 0, 0)
    style.font.bold = name != 'Subtitle'
    style.paragraph_format.space_before = Pt(10 if name not in ['Title', 'Subtitle'] else 0)
    style.paragraph_format.space_after = Pt(7)
    style.paragraph_format.keep_with_next = True
    style.paragraph_format.line_spacing = 1.05
    if style.element.pPr is not None:
        for border in list(style.element.pPr.findall(qn('w:pBdr'))): style.element.pPr.remove(border)

props = doc.core_properties
props.title = 'Đặc tả yêu cầu phần mềm hệ thống quản lý thuê phòng trọ'
props.subject = 'Booking hợp đồng hóa đơn thanh toán và trả phòng'
props.author = 'Nhóm dự án hệ thống quản lý thuê phòng trọ'
props.keywords = 'SRS, thuê phòng trọ, booking, hợp đồng, hóa đơn, trả phòng'

sec.different_first_page_header_footer = True
head = sec.header.paragraphs[0]
head.text = 'HỆ THỐNG QUẢN LÝ THUÊ PHÒNG TRỌ    |    SRS 1.0'
head.runs[0].font.size = Pt(9)
head.runs[0].font.color.rgb = RGBColor(0, 0, 0)
foot = sec.footer.paragraphs[0]
foot.alignment = WD_ALIGN_PARAGRAPH.RIGHT
foot.add_run('Trang ')
for field, suffix in [('PAGE', ' / '), ('NUMPAGES', '')]:
    r = OxmlElement('w:r')
    f = OxmlElement('w:fldSimple'); f.set(qn('w:instr'), field)
    rtext = OxmlElement('w:r'); t = OxmlElement('w:t'); t.text = '1'; rtext.append(t); f.append(rtext)
    foot._p.append(f)
    if suffix: foot.add_run(suffix)
for r in foot.runs: r.font.size = Pt(9)

def text(p, value):
    # Keep API path wrapping legible without changing the visible route text.
    for part in re.split(r'(\*\*.*?\*\*)', value):
        run = p.add_run(part[2:-2] if part.startswith('**') else part)
        run.bold = part.startswith('**')

def table(rows):
    n = len(rows[0])
    widths = {2: [3.2, 13.8], 3: [1.65, 13.9, 1.45], 4: [7.3, 3.4, 3.4, 2.9]}[n]
    # Semantic column widths for repeated schemas.
    if rows[0][0] == 'Bước': widths = [3.3, 4.0, 9.7]
    elif rows[0][0] == 'Mã' and rows[0][-1] == 'Liên kết': widths = [1.5, 11.9, 3.6]
    elif rows[0][0] == 'Mã' and rows[0][-1] == 'Ảnh hưởng': widths = [1.5, 11.5, 4.0]
    elif rows[0][0] == 'Mã' and rows[0][-1] == 'Ưu tiên': widths = [1.8, 13.2, 2.0]
    elif rows[0][0] in ['Thuật ngữ', 'Tác nhân', 'Nhóm dữ liệu', 'Nhóm']: widths = [4.0, 13.0]
    elif rows[0][0] == 'Mã' and n == 2: widths = [1.8, 15.2]
    tab = doc.add_table(rows=0, cols=n)
    tab.alignment = WD_TABLE_ALIGNMENT.CENTER
    tab.autofit = False
    for col, width in zip(tab.columns, widths): col.width = Cm(width)
    pr = tab._tbl.tblPr
    borders = OxmlElement('w:tblBorders')
    for edge in ['top', 'left', 'bottom', 'right', 'insideH', 'insideV']:
        el = OxmlElement('w:' + edge)
        for key, val in [('val', 'single'), ('sz', '4'), ('color', 'D9D9D9')]: el.set(qn('w:'+key), val)
        borders.append(el)
    pr.append(borders)
    for ri, row in enumerate(rows):
        cells = tab.add_row().cells
        trpr = cells[0]._tc.getparent().get_or_add_trPr()
        if ri == 0: trpr.append(OxmlElement('w:tblHeader'))
        trpr.append(OxmlElement('w:cantSplit'))
        for ci, (cell, value, width) in enumerate(zip(cells, row, widths)):
            cell.width = Cm(width)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            tcpr = cell._tc.get_or_add_tcPr()
            mar = OxmlElement('w:tcMar')
            for side in ['top', 'bottom', 'left', 'right']:
                m = OxmlElement('w:'+side); m.set(qn('w:w'), '70' if side in ['top','bottom'] else '95'); m.set(qn('w:type'), 'dxa'); mar.append(m)
            tcpr.append(mar)
            shade = OxmlElement('w:shd'); shade.set(qn('w:fill'), 'E8EDF2' if ri == 0 else ('F7F8FA' if ri%2 == 0 else 'FFFFFF')); tcpr.append(shade)
            p = cell.paragraphs[0]
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.line_spacing = 1.04
            if (ci == 0 and widths[0] < 2.1) or (ci == n-1 and widths[-1] <= 2.1): p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            text(p, value)
            for run in p.runs:
                run.font.size = Pt(10.5 if '/api/' in value else 11)
                run.font.color.rgb = RGBColor(0,0,0)
                if ri == 0: run.bold = True
    doc.add_paragraph().paragraph_format.space_after = Pt(1)

pages = SOURCE.read_text(encoding='utf-8').split('<!-- PAGE -->')
for pi, page in enumerate(pages):
    if pi: doc.add_page_break()
    lines = page.strip().splitlines()
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if not line:
            i += 1; continue
        if line.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].strip().startswith('|'):
                vals = [x.strip() for x in lines[i].strip().strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?', x) for x in vals): rows.append(vals)
                i += 1
            table(rows); continue
        if line.startswith('# '):
            doc.add_paragraph(line[2:], 'Title' if pi == 0 else 'Heading 1')
        elif line.startswith('## '): doc.add_paragraph(line[3:], 'Subtitle' if pi == 0 else 'Heading 2')
        elif line.startswith('### '): doc.add_paragraph(line[4:], 'Heading 2')
        elif line.startswith('- '):
            p = doc.add_paragraph(style='List Bullet'); text(p, line[2:]); p.paragraph_format.space_after = Pt(3)
        elif re.match(r'^\d+\. ', line):
            p = doc.add_paragraph(); p.paragraph_format.left_indent = Cm(.5); p.paragraph_format.first_line_indent = Cm(-.5); text(p, line)
        else:
            p = doc.add_paragraph(); text(p, line)
        i += 1

OUT.parent.mkdir(parents=True, exist_ok=True)
doc.save(OUT)
print(OUT)
print('Designed sections:', len(pages))
