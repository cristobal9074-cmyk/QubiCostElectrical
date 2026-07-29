from pathlib import Path

p = Path(__file__).resolve().parents[1] / "src/QubiCostElectrical.Wpf/Features/Export/ExportViewModel.cs"
text = p.read_text(encoding="utf-8", errors="replace")

text = text.replace("\ufffdrea", "Área")
text = text.replace("Exportaci\ufffdn", "Exportación")
text = text.replace("cubicaci\ufffdn", "cubicación")
text = text.replace("acompa\ufffdar", "acompañar")
text = text.replace("aqu\ufffd", "aquí")
text = text.replace(" ? ", " · ")

p.write_text(text, encoding="utf-8")
print("fixed", p)
