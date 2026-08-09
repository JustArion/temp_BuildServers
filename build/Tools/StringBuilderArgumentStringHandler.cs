using System.Runtime.CompilerServices;
using System.Text;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Utilities;

namespace Tools;

[InterpolatedStringHandler]
public readonly struct StringBuilderArgumentStringHandler
{
    private readonly StringBuilder _builder;
    private readonly List<string> _secretValues;

    public StringBuilderArgumentStringHandler(
        int literalLength,
        int formattedCount,
        out bool handlerIsValid)
    {
        _builder = new(literalLength);
        _secretValues = [];
        handlerIsValid = true;
    }

    public static implicit operator StringBuilderArgumentStringHandler(string value)
    {
        var safeValue = value.NotNull();
        var handler = new StringBuilderArgumentStringHandler(safeValue.Length, 0, out var _);
        handler.AppendLiteral(safeValue);
        return handler;
    }

    public void AppendLiteral(string value)
    {
        _builder.Append(value);
    }

    public void AppendFormatted(object? obj, int alignment = 0, string? format = null)
    {
        switch (obj)
        {
            case string value:
            {
                if (format == "r")
                    _secretValues.Add(value);
                else if (!(value.IsDoubleQuoted() || value.IsSingleQuoted() || format == "nq"))
                    (value, format) = (value.DoubleQuoteIfNeeded(), null);
                AppendFormatted(value, alignment, format);
                break;
            }
            case IAbsolutePathHolder holder:
                AppendFormatted(holder, alignment, format);
                break;
            default:
                AppendFormatted(obj?.ToString() ?? string.Empty, alignment, format);
                break;
        }
    }

    private void AppendFormatted(string? value, int alignment, string? format)
    {
        _builder.Append(value); // Note: Manual padding required here if 'alignment' is strictly needed
    }

    private void AppendFormatted(IAbsolutePathHolder holder, int alignment, string? format)
    {
        _builder.Append(holder.Path);
    }

    public void AppendFormatted(IEnumerable<IAbsolutePathHolder> paths, int alignment = 0, string? format = null)
    {
        var list = paths.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            _builder.Append(list[i].Path);
            if (i + 1 < list.Count)
                _builder.Append(' ');
        }
    }

    public string ToStringAndClear()
    {
        var value = _builder.ToString();
        _builder.Clear();
        
        return value.Length > 1 && value.IndexOf('"', 1) == value.Length - 1
            ? value.TrimMatchingDoubleQuotes()
            : value;
    }

    public Func<string, string> GetFilter()
    {
        var secretValues = _secretValues;
        return x => secretValues.Aggregate(x, (arguments, value) => arguments.Replace(value, "[REDACTED]"));
    }
}