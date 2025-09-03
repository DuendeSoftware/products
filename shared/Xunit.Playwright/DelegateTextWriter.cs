// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace Duende.Xunit.Playwright;

public class DelegateTextWriter : TextWriter
{
    private readonly WriteTestOutput _writeAction;
    private string _currentLine;

    public DelegateTextWriter(WriteTestOutput writeAction)
    {
        _writeAction = writeAction ?? throw new ArgumentNullException(nameof(writeAction));
        _currentLine = string.Empty;
    }

    public override Encoding Encoding => Encoding.Default;

    public override void Write(char value)
    {
        if (value == '\r')
        // let's ignore carriage returns
        {
            return;
        }

        if (value == '\n')
        {
            _writeAction(_currentLine);
            _currentLine = string.Empty;
        }
        else
        {
            _currentLine += value;
        }
    }

    public override void Write(string? value)
    {
        if (value == null)
        {
            return;
        }

        _currentLine += value.Replace("\r\n", "\n");

        if (_currentLine.Contains(value: '\n'))
        {
            var lines = _currentLine.Split(new[] { '\n' }, StringSplitOptions.None);

            for (var i = 0; i < lines.Length - 1; i++)
            {
                _writeAction(lines[i]);
            }

            _currentLine = lines[^1];
        }
    }

    public override void WriteLine(string? value)
    {
        if (value == null)
        {
            return;
        }

        _writeAction(value + Environment.NewLine);
        _currentLine = string.Empty;
    }
}
