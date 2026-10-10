using System.Globalization;

namespace Nexus.Shell.Services;

// Decimal arithmetic only. No expression execution, scripts or external app.
public sealed class CalculatorEngine
{
    public string Display { get; private set; } = "0";
    public string Expression { get; private set; } = "";
    public bool HasError { get; private set; }
    private decimal? _left, _repeat;
    private string _operation = "", _repeatOperation = "";
    private bool _fresh = true, _result;
    private decimal Value => decimal.Parse(Display, CultureInfo.InvariantCulture);
    private static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    public void Input(string key)
    {
        try
        {
            if (key == "C") { Clear(); return; }
            if (key.Length == 1 && (char.IsAsciiDigit(key[0]) || key == "."))
            {
                if (HasError || (_result && _operation.Length == 0)) Clear();
                string next = _fresh ? key == "." ? "0." : key : key == "." ? Display.Contains('.') ? Display : Display + "." : Display == "0" ? key : Display + key;
                if (next.Count(char.IsAsciiDigit) <= 28 && decimal.TryParse(next, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                { Display = next; _fresh = _result = false; }
                return;
            }
            if (HasError) return;
            if (key == "Back") { if (_fresh) return; Display = Display.Length <= 1 || Display.Length == 2 && Display[0] == '-' ? "0" : Display[..^1]; return; }
            if (key == "+/−") { Display = Format(-Value); _fresh = false; return; }
            if (key == "%")
            { Display = Format(_left is decimal left && _operation is "+" or "−" ? left * Value / 100 : Value / 100); _fresh = false; return; }
            if (key == "=")
            {
                decimal right = Value;
                if (_operation.Length > 0 && _left is decimal first)
                {
                    if (_fresh) right = first;
                    Display = Format(Calculate(first, _operation, right));
                    Expression = $"{Format(first)} {_operation} {Format(right)} =";
                    _repeat = right; _repeatOperation = _operation;
                }
                else if (_result && _repeat is decimal repeated)
                { Display = Format(Calculate(right, _repeatOperation, repeated)); Expression = $"{Format(right)} {_repeatOperation} {Format(repeated)} ="; }
                _left = null; _operation = ""; _fresh = _result = true; return;
            }
            if (key is "+" or "−" or "×" or "÷")
            {
                if (_left is decimal first && _operation.Length > 0 && !_fresh) Display = Format(Calculate(first, _operation, Value));
                _left = Value; _operation = key; Expression = $"{Display} {key}";
                _fresh = true; _result = false; _repeat = null;
            }
        }
        catch (DivideByZeroException) { Fail("Cannot divide by zero"); }
        catch (OverflowException) { Fail("Number is too large"); }
    }
    private static decimal Calculate(decimal first, string op, decimal second) => op switch
    { "+" => first + second, "−" => first - second, "×" => first * second, "÷" => first / second, _ => second };
    private void Fail(string message) { Clear(); Display = message; HasError = true; }
    private void Clear() { Display = "0"; Expression = ""; HasError = false; _left = _repeat = null; _operation = _repeatOperation = ""; _fresh = true; _result = false; }
}
