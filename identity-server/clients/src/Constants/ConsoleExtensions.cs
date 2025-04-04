// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Clients;

public static class ConsoleExtensions
{
    /// <summary>
    /// Writes green text to the console.
    /// </summary>
    /// <param name="text">The text.</param>
    [DebuggerStepThrough]
    public static void ConsoleGreen(this string text) => text.ColoredWriteLine(ConsoleColor.Green);

    /// <summary>
    /// Writes text to the console in a box.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="color">The color of the text to write.</param>
    [DebuggerStepThrough]
    public static void ConsoleBox(this string text, ConsoleColor color)
    {
        var len = text.Length + 4;
        var line = new string('*', len);
        line.ColoredWriteLine(ConsoleColor.Green);
        $"* {text} *".ColoredWriteLine(ConsoleColor.Green);
        line.ColoredWriteLine(ConsoleColor.Green);
    }

    /// <summary>
    /// Writes red text to the console.
    /// </summary>
    /// <param name="text">The text.</param>
    [DebuggerStepThrough]
    public static void ConsoleRed(this string text) => text.ColoredWriteLine(ConsoleColor.Red);

    /// <summary>
    /// Writes yellow text to the console.
    /// </summary>
    /// <param name="text">The text.</param>
    [DebuggerStepThrough]
    public static void ConsoleYellow(this string text) => text.ColoredWriteLine(ConsoleColor.Yellow);

    /// <summary>
    /// Writes out text with the specified ConsoleColor.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="color">The color.</param>
    [DebuggerStepThrough]
    public static void ColoredWriteLine(this string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
