public class SourceText
{
    public SourceText(string text, string? name = null)
    {
        Text = text;
        Name = name;
    }

    public string Text { get; }
    public string? Name { get; }

    public int GetLineNumberFromOffset(int index)
    {
        var idx = 0;
        var lineNumber = 0;
        while(idx < index)
        {
            idx = Text.IndexOf('\n', idx + 1);
            lineNumber++;
            if(idx < 0)
            {
                idx = Text.Length;
                break;
            }
        }
        return lineNumber;
    }
    public string GetLine(int lineNumber)
    {
        var offsetline = lineNumber - 1;
        var count = 0;
        var index = -1;
        var nextIndex = -1;
        while(offsetline >= count)
        {
            index = nextIndex + 1;
            nextIndex = Text.IndexOf('\n', index);
            if(nextIndex < 0)
            {
                if(offsetline == count) return Text.Substring(index, Text.Length - index).Trim();
                return "";
            }
            count++;
        }
        
        return Text.Substring(index, nextIndex - index).Trim();
    }

    public int GetLineStartOffset(int lineNumber)
    {
        var count = 1;
        var index = 0;
        while(count < lineNumber && index < Text.Length)
        {
            var next = Text.IndexOf('\n', index);
            if(next < 0) return Text.Length;
            index = next + 1;
            count++;
        }
        return index;
    }

    public SourceSpan CreateSpan(int start, int length) => new SourceSpan(this, start, length);

}
