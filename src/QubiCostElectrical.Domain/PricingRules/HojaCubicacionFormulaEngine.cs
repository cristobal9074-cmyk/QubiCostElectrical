using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QubiCostElectrical;

public sealed record HojaCubicacionCellResult(decimal Value, bool Success, string Error, string DisplayText);

public sealed record HojaCubicacionRowResult(IReadOnlyList<HojaCubicacionCellResult> Cells, decimal Suma, string SumaDisplay);

public sealed record HojaCubicacionGridResult(IReadOnlyList<HojaCubicacionRowResult> Rows, decimal Subtotal);

/// <summary>
/// Motor de cálculo de fórmulas para la Hoja de Cubicación tipo Excel con soporte bidimensional (celdas C1, D2, rangos C1:F1, C1:C5, C:E).
/// Soporta funciones matemáticas estándar en español e inglés (SUMA/SUM, PROMEDIO/AVERAGE, MIN, MAX,
/// CONTAR/COUNT, REDONDEAR/ROUND, ENTERO/INT, ABS, RAIZ/SQRT, POTENCIA/POWER, RESIDUO/MOD),
/// inicio con '=' o '+', operadores aritméticos (+, -, *, /, ^, %), porcentajes, y resolución topológica encadenada entre celdas y filas.
/// </summary>
public static class HojaCubicacionFormulaEngine
{
    private const int MaxRecursionDepth = 200;

    /// <summary>
    /// Convierte un índice 0-based de datos (donde 0 = C) en la letra de columna de Excel.
    /// 0 -> "C", 1 -> "D", 2 -> "E", etc.
    /// </summary>
    public static string IndexToColumnLetter(int zeroBasedDataIndex)
    {
        return IndexToExcelColumn(zeroBasedDataIndex + 2); // 0=A, 1=B, 2=C...
    }

    /// <summary>
    /// Convierte una letra de columna de Excel a su índice 0-based de datos.
    /// "C" -> 0, "D" -> 1, "E" -> 2. Retorna -1 si es antes de C (A=Descripción, B=Suma) o inválida.
    /// </summary>
    public static int ColumnLetterToDataIndex(string columnLetter)
    {
        if (string.IsNullOrWhiteSpace(columnLetter)) return -1;
        int col = ExcelColumnToIndex(columnLetter.Trim().ToUpperInvariant());
        return col >= 2 ? col - 2 : -1;
    }

    private static string IndexToExcelColumn(int zeroBasedIndex)
    {
        int dividend = zeroBasedIndex + 1;
        string name = "";
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            name = (char)('A' + modulo) + name;
            dividend = (dividend - modulo - 1) / 26;
        }
        return name;
    }

    private static int ExcelColumnToIndex(string name)
    {
        int sum = 0;
        foreach (char c in name)
        {
            if (c < 'A' || c > 'Z') return -1;
            sum = sum * 26 + (c - 'A' + 1);
        }
        return sum - 1;
    }

    /// <summary>
    /// Evalúa una única fila de datos (como un grid de 1 fila).
    /// </summary>
    public static HojaCubicacionRowResult EvaluateRow(IReadOnlyList<string?> rawValues)
    {
        var grid = new List<IReadOnlyList<string?>> { rawValues };
        var gridResult = EvaluateGrid(grid);
        return gridResult.Rows[0];
    }

    /// <summary>
    /// Evalúa una grilla completa de filas (2D) de un ítem de cubicación.
    /// Permite que fórmulas en cualquier fila referencien celdas de otras filas (ej. C1, C2, D3) o de la misma fila (ej. C, D).
    /// </summary>
    public static HojaCubicacionGridResult EvaluateGrid(IReadOnlyList<IReadOnlyList<string?>> gridRows)
    {
        int rowCount = gridRows.Count;
        var cellResults = new HojaCubicacionCellResult[rowCount][];
        var cellStates = new CellEvalState[rowCount][];

        for (int r = 0; r < rowCount; r++)
        {
            int colCount = gridRows[r].Count;
            cellResults[r] = new HojaCubicacionCellResult[colCount];
            cellStates[r] = new CellEvalState[colCount];
        }

        var accessor = new GridAccessor(gridRows, cellResults, cellStates);

        for (int r = 0; r < rowCount; r++)
        {
            for (int c = 0; c < gridRows[r].Count; c++)
            {
                accessor.EnsureEvaluated(r, c);
            }
        }

        var rowsOutput = new List<HojaCubicacionRowResult>(rowCount);
        decimal grandTotal = 0m;

        for (int r = 0; r < rowCount; r++)
        {
            decimal rowSuma = 0m;
            for (int c = 0; c < cellResults[r].Length; c++)
            {
                var cell = cellResults[r][c];
                if (cell != null && cell.Success)
                    rowSuma += cell.Value;
            }

            string rowSumaDisplay = rowSuma == 0m ? "" : FormatDecimal(rowSuma);
            rowsOutput.Add(new HojaCubicacionRowResult(cellResults[r], rowSuma, rowSumaDisplay));
            grandTotal += rowSuma;
        }

        return new HojaCubicacionGridResult(rowsOutput, grandTotal);
    }

    private enum CellEvalState { Unvisited, Evaluating, Done }

    private sealed class GridAccessor
    {
        private readonly IReadOnlyList<IReadOnlyList<string?>> gridRows;
        private readonly HojaCubicacionCellResult[][] results;
        private readonly CellEvalState[][] states;

        public GridAccessor(
            IReadOnlyList<IReadOnlyList<string?>> gridRows,
            HojaCubicacionCellResult[][] results,
            CellEvalState[][] states)
        {
            this.gridRows = gridRows;
            this.results = results;
            this.states = states;
        }

        public decimal GetCellValue(int rowIndex, int colIndex)
        {
            if (rowIndex < 0 || colIndex < 0)
                throw new FormulaException("Coordenadas de celda no válidas.");

            // Igual que en Excel: celdas no creadas aún o vacías valen 0 numéricamente
            if (rowIndex >= gridRows.Count || colIndex >= gridRows[rowIndex].Count)
                return 0m;

            if (states[rowIndex][colIndex] == CellEvalState.Evaluating)
                throw new CircularReferenceException();

            EnsureEvaluated(rowIndex, colIndex);

            var res = results[rowIndex][colIndex];
            if (res != null && !res.Success)
            {
                if (res.DisplayText == "¡CIRCULAR!")
                    throw new CircularReferenceException();
                throw new FormulaException(res.Error);
            }

            return res?.Value ?? 0m;
        }

        public List<decimal> GetRangeValues(int r1, int c1, int r2, int c2)
        {
            int minR = Math.Max(0, Math.Min(r1, r2));
            int maxR = Math.Min(gridRows.Count - 1, Math.Max(r1, r2));
            int minC = Math.Max(0, Math.Min(c1, c2));
            int maxC = Math.Max(0, Math.Max(c1, c2));

            var values = new List<decimal>();
            for (int r = minR; r <= maxR; r++)
            {
                int currentMaxC = Math.Min(gridRows[r].Count - 1, maxC);
                for (int c = minC; c <= currentMaxC; c++)
                {
                    string raw = (gridRows[r][c] ?? "").Trim();
                    if (raw.Length == 0) continue; // Celdas vacías no aportan a funciones tipo SUM/AVG
                    values.Add(GetCellValue(r, c));
                }
            }
            return values;
        }

        public void EnsureEvaluated(int r, int c)
        {
            if (states[r][c] == CellEvalState.Done) return;

            if (states[r][c] == CellEvalState.Evaluating)
            {
                results[r][c] = new HojaCubicacionCellResult(0m, false, "Referencia circular", "¡CIRCULAR!");
                return;
            }

            states[r][c] = CellEvalState.Evaluating;

            string raw = (gridRows[r][c] ?? "").Trim();
            if (raw.Length == 0)
            {
                results[r][c] = new HojaCubicacionCellResult(0m, true, "", "");
                states[r][c] = CellEvalState.Done;
                return;
            }

            // Si es un número simple directo (ej: "15", "20.5", "30,5")
            if (TryParseLiteral(raw, out decimal literalVal))
            {
                results[r][c] = new HojaCubicacionCellResult(literalVal, true, "", FormatDecimal(literalVal));
                states[r][c] = CellEvalState.Done;
                return;
            }

            // Si es texto libre y NO parece una fórmula (ej: "Dpto 101", "Piso 1", "Cocina")
            if (!IsFormulaLike(raw))
            {
                results[r][c] = new HojaCubicacionCellResult(0m, true, "", raw);
                states[r][c] = CellEvalState.Done;
                return;
            }

            // Es una fórmula tipo Excel: evaluarla
            try
            {
                var parser = new FormulaParser(raw, r, this);
                decimal val = parser.Parse();
                results[r][c] = new HojaCubicacionCellResult(val, true, "", FormatDecimal(val));
            }
            catch (CircularReferenceException)
            {
                results[r][c] = new HojaCubicacionCellResult(0m, false, "Referencia circular", "¡CIRCULAR!");
            }
            catch (DivideByZeroException)
            {
                results[r][c] = new HojaCubicacionCellResult(0m, false, "División por cero", "¡DIV/0!");
            }
            catch (Exception ex)
            {
                results[r][c] = new HojaCubicacionCellResult(0m, false, ex.Message, "¡ERROR!");
            }
            finally
            {
                states[r][c] = CellEvalState.Done;
            }
        }
    }

    public static bool IsFormulaLike(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string text = raw.Trim();
        if (text.StartsWith("=", StringComparison.Ordinal) ||
            text.StartsWith("+", StringComparison.Ordinal) ||
            text.StartsWith("-", StringComparison.Ordinal))
            return true;

        string upper = text.ToUpperInvariant();
        if (upper.Contains("SUMA(") || upper.Contains("SUM(") || upper.Contains("PROMEDIO(") ||
            upper.Contains("AVERAGE(") || upper.Contains("MIN(") || upper.Contains("MAX(") ||
            upper.Contains("REDONDEAR(") || upper.Contains("ROUND(") || upper.Contains("CONTAR(") ||
            upper.Contains("COUNT(") || upper.Contains("ABS(") || upper.Contains("RAIZ(") ||
            upper.Contains("SQRT(") || upper.Contains("POTENCIA(") || upper.Contains("POWER(") ||
            upper.Contains("PRODUCTO(") || upper.Contains("PRODUCT(") || upper.Contains("MULTIPLICAR(") ||
            upper.Contains("MOD(") || upper.Contains("RESIDUO(") || upper.Contains("ENTERO(") || upper.Contains("INT(") ||
            upper.Contains("MEDIANA(") || upper.Contains("MEDIAN(") || upper.Contains("LOG(") || upper.Contains("LN(") ||
            upper.Contains("SENO(") || upper.Contains("SIN(") || upper.Contains("COS(") || upper.Contains("COSENO(") ||
            upper.Contains("TAN(") || upper.Contains("TANGENTE("))
            return true;

        // Si contiene operadores multiplicativos, divisores o de potencia
        if (text.Any(c => c == '*' || c == '/' || c == '^' || c == '%'))
            return true;

        // Si contiene '+' o '-' junto con celdas (ej. C1-H2, C1+D1) o números (ej. 50-20, 50+10)
        if (text.Contains('+') || text.Contains('-'))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(upper, @"[A-Z]+\d*\s*[\+\-]\s*[A-Z0-9]+"))
                return true;
            if (System.Text.RegularExpressions.Regex.IsMatch(upper, @"\d+\s*[\+\-]\s*\d+"))
                return true;
        }

        // Si contiene paréntesis matemáticos con números o letras
        if (text.Contains('(') && text.Contains(')') && (text.Any(char.IsDigit) || text.Any(char.IsLetter)))
            return true;

        return false;
    }

    public static string FormatDecimal(decimal value)
    {
        decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounded % 1 == 0
            ? rounded.ToString("0", CultureInfo.CurrentCulture)
            : rounded.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static bool TryParseLiteral(string raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string text = raw.Trim();
        if (text.StartsWith("=", StringComparison.Ordinal) || text.StartsWith("+", StringComparison.Ordinal))
            return false;

        return decimal.TryParse(text.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parser recursivo descendente tipo Excel con soporte para celdas 2D (C1, D2, etc.) y rangos (C1:F1, C:E).
    /// </summary>
    private sealed class FormulaParser
    {
        private readonly string text;
        private readonly int currentRow;
        private readonly GridAccessor accessor;
        private int pos;
        private int depth;

        public FormulaParser(string rawExpression, int currentRow, GridAccessor accessor)
        {
            string clean = (rawExpression ?? "").Trim();
            if (clean.StartsWith("=", StringComparison.Ordinal)) clean = clean[1..].Trim();
            if (clean.StartsWith("+", StringComparison.Ordinal)) clean = clean[1..].Trim();
            if (clean.Contains('$')) clean = clean.Replace("$", "");

            this.text = clean;
            this.currentRow = currentRow;
            this.accessor = accessor;
            this.pos = 0;
            this.depth = 0;
        }

        public decimal Parse()
        {
            if (string.IsNullOrWhiteSpace(text)) return 0m;
            decimal val = ParseExpression();
            SkipWhiteSpace();
            if (pos < text.Length)
                throw new FormulaException($"Carácter inesperado: '{text[pos]}'.");
            return val;
        }

        private decimal ParseExpression()
        {
            EnterRecursion();
            try
            {
                decimal value = ParseTerm();
                while (true)
                {
                    SkipWhiteSpace();
                    if (Match('+')) value += ParseTerm();
                    else if (Match('-')) value -= ParseTerm();
                    else break;
                }
                return value;
            }
            finally
            {
                depth--;
            }
        }

        private decimal ParseTerm()
        {
            decimal value = ParsePower();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('*')) value *= ParsePower();
                else if (Match('/'))
                {
                    decimal divisor = ParsePower();
                    if (divisor == 0m) throw new DivideByZeroException();
                    value /= divisor;
                }
                else break;
            }
            return value;
        }

        private decimal ParsePower()
        {
            decimal value = ParseUnary();
            SkipWhiteSpace();
            if (Match('^'))
            {
                EnterRecursion();
                try
                {
                    decimal exponent = ParsePower();
                    double pow = Math.Pow((double)value, (double)exponent);
                    if (double.IsNaN(pow) || double.IsInfinity(pow))
                        throw new FormulaException("Resultado numérico inválido.");
                    value = Convert.ToDecimal(pow);
                }
                finally
                {
                    depth--;
                }
            }
            return value;
        }

        private decimal ParseUnary()
        {
            SkipWhiteSpace();
            if (Match('+')) return ParseUnary();
            if (Match('-')) return -ParseUnary();
            return ParsePostfix();
        }

        private decimal ParsePostfix()
        {
            decimal value = ParsePrimary();
            SkipWhiteSpace();
            while (Match('%'))
            {
                value /= 100m;
                SkipWhiteSpace();
            }
            return value;
        }

        private decimal ParsePrimary()
        {
            SkipWhiteSpace();
            if (Match('('))
            {
                decimal value = ParseExpression();
                SkipWhiteSpace();
                if (!Match(')')) throw new FormulaException("Falta cerrar paréntesis ')'.");
                return value;
            }

            if (!IsAtEnd && (char.IsLetter(text[pos]) || text[pos] == '_'))
            {
                return ParseIdentifierOrFunction();
            }

            return ParseNumber();
        }

        private decimal ParseIdentifierOrFunction()
        {
            int start = pos;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                pos++;

            string identifier = text.Substring(start, pos - start);
            SkipWhiteSpace();

            if (Match('('))
            {
                return ExecuteFunction(identifier.ToUpperInvariant());
            }

            if (Match(':') || MatchRangeDots())
            {
                int rangeEndStart = pos;
                while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                    pos++;
                string rangeEnd = text.Substring(rangeEndStart, pos - rangeEndStart);
                var vals = ResolveRange(identifier, rangeEnd);
                return vals.Count > 0 ? vals.Sum() : 0m;
            }

            return ResolveCellReference(identifier);
        }

        private bool MatchRangeDots()
        {
            if (pos + 1 < text.Length && text[pos] == '.' && text[pos + 1] == '.')
            {
                pos += 2;
                return true;
            }
            return false;
        }

        private decimal ExecuteFunction(string fn)
        {
            var args = new List<decimal>();

            SkipWhiteSpace();
            if (Match(')'))
            {
                return EvaluateFunctionValues(fn, args);
            }

            while (true)
            {
                SkipWhiteSpace();
                int savedPos = pos;
                if (TryConsumeRange(out var rangeValues))
                {
                    args.AddRange(rangeValues);
                }
                else
                {
                    pos = savedPos;
                    decimal argVal = ParseExpression();
                    args.Add(argVal);
                }

                SkipWhiteSpace();
                if (Match(';') || Match(','))
                {
                    continue;
                }
                if (Match(')'))
                {
                    break;
                }

                throw new FormulaException($"Se esperaba separador ',' o ';' o cierre ')' en función {fn}.");
            }

            return EvaluateFunctionValues(fn, args);
        }

        private bool TryConsumeRange(out List<decimal> rangeValues)
        {
            rangeValues = new List<decimal>();
            int start = pos;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                pos++;

            if (pos == start) return false;
            string firstPart = text.Substring(start, pos - start);

            SkipWhiteSpace();
            if (!(Match(':') || MatchRangeDots()))
            {
                pos = start;
                return false;
            }

            SkipWhiteSpace();
            int secondStart = pos;
            while (!IsAtEnd && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                pos++;

            if (secondStart == pos)
            {
                pos = start;
                return false;
            }

            string secondPart = text.Substring(secondStart, pos - secondStart);
            try
            {
                rangeValues = ResolveRange(firstPart, secondPart);
                return true;
            }
            catch
            {
                pos = start;
                return false;
            }
        }

        private decimal EvaluateFunctionValues(string fn, List<decimal> args)
        {
            switch (fn)
            {
                case "SUM":
                case "SUMA":
                    return args.Sum();

                case "AVERAGE":
                case "PROMEDIO":
                    return args.Count == 0 ? 0m : args.Average();

                case "MIN":
                    return args.Count == 0 ? 0m : args.Min();

                case "MAX":
                    return args.Count == 0 ? 0m : args.Max();

                case "COUNT":
                case "CONTAR":
                    return args.Count;

                case "ROUND":
                case "REDONDEAR":
                    if (args.Count == 0) return 0m;
                    int decimals = args.Count > 1 ? (int)Math.Max(0, Math.Min(10, args[1])) : 0;
                    return Math.Round(args[0], decimals, MidpointRounding.AwayFromZero);

                case "INT":
                case "ENTERO":
                    return args.Count == 0 ? 0m : Math.Floor(args[0]);

                case "ABS":
                    return args.Count == 0 ? 0m : Math.Abs(args[0]);

                case "SQRT":
                case "RAIZ":
                    if (args.Count == 0) return 0m;
                    if (args[0] < 0m) throw new FormulaException("Raíz de número negativo no permitida.");
                    return Convert.ToDecimal(Math.Sqrt((double)args[0]));

                case "POWER":
                case "POTENCIA":
                    if (args.Count < 2) throw new FormulaException("POTENCIA requiere 2 argumentos (base, exponente).");
                    double p = Math.Pow((double)args[0], (double)args[1]);
                    return Convert.ToDecimal(p);

                case "PRODUCT":
                case "PRODUCTO":
                case "MULTIPLICAR":
                    if (args.Count == 0) return 0m;
                    decimal prod = 1m;
                    foreach (var a in args) prod *= a;
                    return prod;

                case "MEDIAN":
                case "MEDIANA":
                    if (args.Count == 0) return 0m;
                    var sorted = args.OrderBy(x => x).ToList();
                    int mid = sorted.Count / 2;
                    return (sorted.Count % 2 != 0) ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;

                case "PI":
                    return 3.14159265358979323846m;

                case "EXP":
                    if (args.Count == 0) return 1m;
                    return Convert.ToDecimal(Math.Exp((double)args[0]));

                case "LN":
                    if (args.Count == 0 || args[0] <= 0m) throw new FormulaException("LN requiere número positivo.");
                    return Convert.ToDecimal(Math.Log((double)args[0]));

                case "LOG":
                case "LOG10":
                    if (args.Count == 0 || args[0] <= 0m) throw new FormulaException("LOG requiere número positivo.");
                    double baseLog = args.Count > 1 ? (double)args[1] : 10.0;
                    return Convert.ToDecimal(Math.Log((double)args[0], baseLog));

                case "SIN":
                case "SENO":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal(Math.Sin((double)args[0]));

                case "COS":
                case "COSENO":
                    if (args.Count == 0) return 1m;
                    return Convert.ToDecimal(Math.Cos((double)args[0]));

                case "TAN":
                case "TANGENTE":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal(Math.Tan((double)args[0]));

                case "ASIN":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal(Math.Asin((double)args[0]));

                case "ACOS":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal(Math.Acos((double)args[0]));

                case "ATAN":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal(Math.Atan((double)args[0]));

                case "DEGREES":
                case "GRADOS":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal((double)args[0] * (180.0 / Math.PI));

                case "RADIANS":
                case "RADIANES":
                    if (args.Count == 0) return 0m;
                    return Convert.ToDecimal((double)args[0] * (Math.PI / 180.0));

                default:
                    throw new FormulaException($"Función desconocida: {fn}.");
            }
        }

        private decimal ResolveCellReference(string identifier)
        {
            ParseCellAddress(identifier, currentRow, out int targetRow, out int targetCol);
            return accessor.GetCellValue(targetRow, targetCol);
        }

        private List<decimal> ResolveRange(string fromId, string toId)
        {
            ParseCellAddress(fromId, currentRow, out int r1, out int c1);
            ParseCellAddress(toId, currentRow, out int r2, out int c2);

            return accessor.GetRangeValues(r1, c1, r2, c2);
        }

        private static void ParseCellAddress(string token, int defaultRow, out int rowIndex, out int colIndex)
        {
            string trimmed = token.Trim().ToUpperInvariant();
            int i = 0;
            while (i < trimmed.Length && char.IsLetter(trimmed[i])) i++;

            if (i == 0)
                throw new FormulaException($"Referencia de celda inválida: {token}.");

            string colLetters = trimmed.Substring(0, i);
            colIndex = ColumnLetterToDataIndex(colLetters);
            if (colIndex < 0)
                throw new FormulaException($"Columna no reconocida o reservada: {colLetters}.");

            string rowPart = trimmed.Substring(i);
            if (rowPart.Length > 0 && int.TryParse(rowPart, out int rNum))
            {
                rowIndex = rNum - 1; // 1-based to 0-based
            }
            else
            {
                rowIndex = defaultRow;
            }
        }

        private decimal ParseNumber()
        {
            SkipWhiteSpace();
            int start = pos;
            bool hasDigit = false;
            bool hasSeparator = false;

            while (!IsAtEnd)
            {
                char ch = text[pos];
                if (char.IsDigit(ch))
                {
                    hasDigit = true;
                    pos++;
                }
                else if ((ch == '.' || ch == ',') && !hasSeparator)
                {
                    if (ch == '.' && pos + 1 < text.Length && text[pos + 1] == '.')
                        break;

                    hasSeparator = true;
                    pos++;
                }
                else
                {
                    break;
                }
            }

            if (!hasDigit)
                throw new FormulaException($"Se esperaba número o expresión cerca de '{PeekRemaining()}'.");

            string rawNum = text.Substring(start, pos - start).Replace(',', '.');
            if (!decimal.TryParse(rawNum, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal val))
                throw new FormulaException($"Número inválido: {rawNum}.");

            return val;
        }

        private void EnterRecursion()
        {
            depth++;
            if (depth > MaxRecursionDepth)
                throw new FormulaException("Fórmula excesivamente anidada.");
        }

        public void SkipWhiteSpace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(text[pos])) pos++;
        }

        private bool Match(char expected)
        {
            if (IsAtEnd || text[pos] != expected) return false;
            pos++;
            return true;
        }

        private bool IsAtEnd => pos >= text.Length;

        private string PeekRemaining()
        {
            if (pos >= text.Length) return "fin";
            int len = Math.Min(12, text.Length - pos);
            return text.Substring(pos, len);
        }
    }

    private sealed class CircularReferenceException : Exception { }

    private sealed class FormulaException : Exception
    {
        public FormulaException(string message) : base(message) { }
    }
}
