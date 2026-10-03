using System.Globalization;
using System.Text;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

/// <summary>
/// P2-30: minimal single-page PDF 1.4 writer (Helvetica, WinAnsi). The xref
/// table and startxref are computed from the real byte offsets of each
/// object — hard-coded offsets produce a file readers must "repair".
/// No dependency: the reports are a handful of text lines.
/// </summary>
public static class SimplePdf
{
    public sealed record Line(string Text, int FontSize = 11, int SpaceBefore = 20);

    // Latin-1 matches WinAnsiEncoding for every German character we print.
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static byte[] Build(IEnumerable<Line> lines, int startY = 780)
    {
        var content = new StringBuilder();
        content.Append("BT\n");
        var first = true;
        foreach (var line in lines)
        {
            content.Append(CultureInfo.InvariantCulture, $"/F1 {line.FontSize} Tf\n");
            content.Append(first
                ? string.Create(CultureInfo.InvariantCulture, $"50 {startY} Td\n")
                : string.Create(CultureInfo.InvariantCulture, $"0 {-line.SpaceBefore} Td\n"));
            content.Append('(').Append(Escape(line.Text)).Append(") Tj\n");
            first = false;
        }
        content.Append("ET\n");

        var stream = Latin1.GetBytes(content.ToString());

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            null, // 4: content stream, written below
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        };

        using var output = new MemoryStream();
        var offsets = new long[objects.Length];

        Write(output, "%PDF-1.4\n");
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = output.Position;
            var number = i + 1;
            if (objects[i] is { } body)
            {
                Write(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));
            }
            else
            {
                Write(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n<< /Length {stream.Length} >>\nstream\n"));
                output.Write(stream);
                Write(output, "\nendstream\nendobj\n");
            }
        }

        var xrefOffset = output.Position;
        var xref = new StringBuilder();
        xref.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n");
        xref.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            xref.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        xref.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        Write(output, xref.ToString());

        return output.ToArray();
    }

    private static void Write(Stream stream, string text) => stream.Write(Latin1.GetBytes(text));

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '(' or ')' or '\\') sb.Append('\\');
            // Anything outside Latin-1 would silently become '?' — replace explicitly.
            sb.Append(c <= 'ÿ' && !char.IsControl(c) ? c : '?');
        }
        return sb.ToString();
    }
}
