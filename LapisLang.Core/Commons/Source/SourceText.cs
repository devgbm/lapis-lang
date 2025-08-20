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
        var count = 0;
        var index = -1;
        var nextIndex = -1;
        while(lineNumber >= count)
        {
            index = nextIndex + 1;
            nextIndex = Text.IndexOf('\n', index);
            if(nextIndex < 0)
            {
                if(lineNumber == count) return Text.Substring(index, Text.Length - index).Trim();
                return "";
            }
            count++;
        }
        
        return Text.Substring(index, nextIndex - index).Trim();
    }

    public SourceSpan CreateSpan(int start, int length) => new SourceSpan(this, start, length);

}
