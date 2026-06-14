using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlexRender.Parsing;

/// <summary>
/// The kind of a parsed path command.
/// </summary>
public enum PathCommandKind
{
    /// <summary>Move the pen to a point (M).</summary>
    MoveTo,

    /// <summary>Draw a straight line to a point (L).</summary>
    LineTo,

    /// <summary>Draw a quadratic Bézier curve (Q): one control point, one end point.</summary>
    QuadTo,

    /// <summary>Draw a cubic Bézier curve (C): two control points, one end point.</summary>
    CubicTo,

    /// <summary>Close the current sub-path (Z).</summary>
    Close
}

/// <summary>
/// A 2D point in absolute path coordinates.
/// </summary>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
public readonly record struct PathPoint(float X, float Y);

/// <summary>
/// A single parsed path command with its associated points.
/// </summary>
/// <param name="Kind">The command kind.</param>
/// <param name="Points">The command's points (empty for <see cref="PathCommandKind.Close"/>).</param>
public sealed record PathCommand(PathCommandKind Kind, IReadOnlyList<PathPoint> Points);

/// <summary>
/// Thrown when path data ('d' attribute) cannot be parsed.
/// </summary>
public sealed class PathParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PathParseException(string message) : base(message)
    {
    }
}

/// <summary>
/// Hand-written tokenizer for SVG-style path data restricted to absolute commands
/// M, L, Q, C, Z. AOT-safe — no regex, no backtracking.
/// </summary>
/// <remarks>
/// Only absolute commands are accepted; lowercase (relative) commands are rejected with an error.
/// Numbers may be separated by whitespace and/or commas.
/// Implicit repeated commands are supported per SVG semantics (e.g. "L 1 1 2 2" is two
/// line-to commands).
/// </remarks>
public static class PathDataParser
{
    /// <summary>
    /// Parses path data into an ordered list of absolute commands.
    /// </summary>
    /// <param name="data">The path data string (e.g. "M 0 0 L 100 50 Z").</param>
    /// <param name="maxCommands">The maximum number of commands to accept before failing, guarding against unbounded input.</param>
    /// <returns>The parsed commands in order. Empty when <paramref name="data"/> is blank.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="PathParseException">
    /// Thrown on malformed input, naming the command and position, or when the number of commands
    /// exceeds <paramref name="maxCommands"/>.
    /// </exception>
    public static IReadOnlyList<PathCommand> Parse(string data, int maxCommands = 10000)
    {
        ArgumentNullException.ThrowIfNull(data);

        var commands = new List<PathCommand>();
        var i = 0;
        var length = data.Length;
        char currentCommand = '\0';

        while (i < length)
        {
            SkipSeparators(data, ref i);
            if (i >= length)
                break;

            var c = data[i];
            var upper = char.ToUpperInvariant(c);

            if (upper is 'M' or 'L' or 'Q' or 'C' or 'Z')
            {
                if (c != upper)
                {
                    throw new PathParseException(
                        $"Relative path command '{c}' at position {i} is not supported; use absolute commands M, L, Q, C, Z.");
                }
                currentCommand = upper;
                i++;

                if (currentCommand == 'Z')
                {
                    commands.Add(new PathCommand(PathCommandKind.Close, Array.Empty<PathPoint>()));
                    if (commands.Count > maxCommands)
                    {
                        throw new PathParseException(
                            $"Path data exceeds the maximum of {maxCommands} commands.");
                    }
                    currentCommand = '\0';
                }
                continue;
            }

            // Not a command letter: must be a coordinate continuing the current command.
            if (currentCommand == '\0')
            {
                throw new PathParseException(
                    $"Unexpected character '{c}' at position {i}: path data must begin with a command letter (M, L, Q, C, Z).");
            }

            if (!IsCoordinateStart(c))
            {
                throw new PathParseException(
                    $"Unexpected character '{c}' at position {i} while reading command '{currentCommand}'.");
            }

            var (kind, pointCount) = currentCommand switch
            {
                'M' => (PathCommandKind.MoveTo, 1),
                'L' => (PathCommandKind.LineTo, 1),
                'Q' => (PathCommandKind.QuadTo, 2),
                'C' => (PathCommandKind.CubicTo, 3),
                _ => throw new PathParseException(
                    $"Internal error: unexpected command '{currentCommand}' at position {i}.")
            };

            var points = new PathPoint[pointCount];
            for (var p = 0; p < pointCount; p++)
            {
                var x = ReadNumber(data, ref i, currentCommand);
                var y = ReadNumber(data, ref i, currentCommand);
                points[p] = new PathPoint(x, y);
            }

            commands.Add(new PathCommand(kind, points));
            if (commands.Count > maxCommands)
            {
                throw new PathParseException(
                    $"Path data exceeds the maximum of {maxCommands} commands.");
            }

            // After an initial MoveTo, repeated coordinates imply LineTo (SVG semantics).
            if (currentCommand == 'M')
            {
                currentCommand = 'L';
            }
        }

        return commands;
    }

    private static bool IsCoordinateStart(char c)
        => c is '-' or '+' or '.' || (c >= '0' && c <= '9');

    private static void SkipSeparators(string data, ref int i)
    {
        while (i < data.Length)
        {
            var c = data[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ',')
            {
                i++;
                continue;
            }
            break;
        }
    }

    private static float ReadNumber(string data, ref int i, char command)
    {
        SkipSeparators(data, ref i);

        var start = i;
        var length = data.Length;

        if (i < length && (data[i] == '-' || data[i] == '+'))
            i++;

        var hasDigits = false;
        while (i < length && data[i] >= '0' && data[i] <= '9')
        {
            i++;
            hasDigits = true;
        }

        if (i < length && data[i] == '.')
        {
            i++;
            while (i < length && data[i] >= '0' && data[i] <= '9')
            {
                i++;
                hasDigits = true;
            }
        }

        // Exponent (e.g. 1e3, 2.5E-2)
        if (i < length && (data[i] == 'e' || data[i] == 'E'))
        {
            i++;
            if (i < length && (data[i] == '-' || data[i] == '+'))
                i++;
            while (i < length && data[i] >= '0' && data[i] <= '9')
                i++;
        }

        if (!hasDigits)
        {
            throw new PathParseException(
                $"Expected a number at position {start} while reading command '{command}'.");
        }

        var span = data.AsSpan(start, i - start);
        if (!float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new PathParseException(
                $"Invalid number '{span.ToString()}' at position {start} while reading command '{command}'.");
        }

        if (!float.IsFinite(value))
        {
            throw new PathParseException(
                $"Number '{span.ToString()}' at position {start} is not finite while reading command '{command}'.");
        }

        return value;
    }
}
