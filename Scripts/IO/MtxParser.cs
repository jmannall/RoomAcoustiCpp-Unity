// This is a parser for Matrix Market files, adapted from MatrixMarketReader.cs.
// The original MatrixMarketReader.cs is under MIT license, with the following copytight info:

// <copyright file="MatrixMarketReader.cs" company="Math.NET">
// Math.NET Numerics, part of the Math.NET Project
// http://numerics.mathdotnet.com
// http://github.com/mathnet/mathnet-numerics
// http://mathnetnumerics.codeplex.com
//
// Copyright (c) 2009-2014 Math.NET
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// </copyright>

using System;
using System.Globalization;
using System.IO;

public static class MtxParser
{
    // Minimal set of symmetry kinds we will handle.
    private enum SymmetryKind
    {
        General,
        Symmetric,
        SkewSymmetric
    }

    // Read a .mtx file into a native 2D int array.
    public static int[,] toArray(string[] lines, out int numNonZero)
    {
        // Validate input.
        if (lines == null)
        {
            throw new ArgumentNullException("lines");
        }

        if (lines.Length == 0)
        {
            throw new InvalidDataException("Empty input. Expected Matrix Market header.");
        }

        // Use InvariantCulture to avoid locale issues when parsing numbers.
        CultureInfo culture = CultureInfo.InvariantCulture;

        // Line cursor to walk through the array of lines.
        int cursor = 0;

        // Read and validate the header line.
        // Example: "%%MatrixMarket matrix coordinate integer general"
        string headerLine = lines[cursor];
        if (headerLine == null)
        {
            throw new InvalidDataException("First line is null. Expected Matrix Market header.");
        }

        string trimmedHeader = headerLine.Trim();
        if (!trimmedHeader.StartsWith("%%MatrixMarket", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Missing or invalid Matrix Market header line.");
        }

        // Split the header into tokens and validate the essential parts.
        // Tokens: ["%%MatrixMarket", "matrix", "coordinate", "integer", "<symmetry>"]
        string[] headerParts = trimmedHeader.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        if (headerParts.Length < 5)
        {
            throw new InvalidDataException("Incomplete Matrix Market header.");
        }

        // Ensure the object type is "matrix".
        if (!headerParts[1].Equals("matrix", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only 'matrix' objects are supported.");
        }

        // Ensure the storage scheme is "coordinate" (sparse).
        if (!headerParts[2].Equals("coordinate", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only 'coordinate' (sparse) format is supported.");
        }

        // Ensure the data type is "integer".
        if (!headerParts[3].Equals("integer", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only 'integer' value type is supported.");
        }

        // Parse symmetry.
        SymmetryKind symmetry = ParseSymmetry(headerParts[4]);

        // Advance cursor to the next line after the header.
        cursor = cursor + 1;

        // Skip comment lines (starting with '%') and empty lines until we reach the size line.
        // The size line for coordinate format has 3 integers: rows cols nnz
        string sizeLine = ReadNextNonCommentLine(lines, ref cursor);
        if (sizeLine == null)
        {
            throw new InvalidDataException("Unexpected end of input while looking for the size line.");
        }

        string[] sizeParts = sizeLine.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (sizeParts.Length < 3)
        {
            throw new InvalidDataException("Size line must contain three integers: rows cols nnz.");
        }

        int rows = int.Parse(sizeParts[0], culture);
        int cols = int.Parse(sizeParts[1], culture);
        int nnz = int.Parse(sizeParts[2], culture);

        if (rows <= 0 || cols <= 0 || nnz < 0)
        {
            throw new InvalidDataException("Invalid matrix dimensions or nnz.");
        }
        numNonZero = nnz;

        // Create the dense 2D array and fill with zeros.
        int[,] result = new int[rows, cols];

        // Read exactly nnz entries.
        // Each entry line has: row col value
        for (int k = 0; k < nnz; k++)
        {
            string entryLine = ReadNextNonEmptyLine(lines, ref cursor);
            if (entryLine == null)
            {
                throw new InvalidDataException("Unexpected end of input while reading entries.");
            }

            string[] parts = entryLine.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                throw new InvalidDataException("Entry line must contain row, col, and value.");
            }

            int i1 = int.Parse(parts[0], culture);
            int j1 = int.Parse(parts[1], culture);
            int v = int.Parse(parts[2], culture);

            // Convert 1-based indices in the file to 0-based indices in the array.
            int i = i1 - 1;
            int j = j1 - 1;

            if (i < 0 || i >= rows || j < 0 || j >= cols)
            {
                throw new InvalidDataException("Entry index out of bounds.");
            }

            // If multiple entries refer to the same position, accumulate them (common MM usage).
            result[i, j] += v;

            // Expand by symmetry when needed.
            if (symmetry == SymmetryKind.Symmetric)
            {
                // For Symmetric: reflect to (j, i) unless on the diagonal.
                if (i != j)
                {
                    result[j, i] += v;
                }
            }
            else if (symmetry == SymmetryKind.SkewSymmetric)
            {
                // For SkewSymmetric: reflect to (j, i) as negative.
                // Diagonal must be zero.
                if (i == j)
                {
                    result[i, j] = 0;
                }
                else
                {
                    result[j, i] -= v;
                }
            }
        }

        return result;
    }

    // Parse the symmetry token from the header.
    private static SymmetryKind ParseSymmetry(string token)
    {
        if (token.Equals("general", StringComparison.OrdinalIgnoreCase))
        {
            return SymmetryKind.General;
        }

        if (token.Equals("symmetric", StringComparison.OrdinalIgnoreCase))
        {
            return SymmetryKind.Symmetric;
        }

        if (token.Equals("skew-symmetric", StringComparison.OrdinalIgnoreCase))
        {
            return SymmetryKind.SkewSymmetric;
        }

        // Hermitian and complex are not supported (we assume integer matrices).
        throw new NotSupportedException("Unsupported symmetry: " + token);
    }

    // Read the next non-comment, non-empty line.
    // Comment lines in Matrix Market start with '%'.
    // Returns null if the end of the array is reached.
    private static string ReadNextNonCommentLine(string[] lines, ref int cursor)
    {
        while (cursor < lines.Length)
        {
            string current = lines[cursor];
            cursor = cursor + 1;

            if (current == null)
            {
                continue;
            }

            string trimmed = current.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("%"))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }

    // Read the next non-empty, non-comment line starting at the current cursor.
    // Returns null if the end of the array is reached.
    private static string ReadNextNonEmptyLine(string[] lines, ref int cursor)
    {
        while (cursor < lines.Length)
        {
            string current = lines[cursor];
            cursor = cursor + 1;

            if (current == null)
            {
                continue;
            }

            string trimmed = current.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("%"))
            {
                continue;
            }

            return trimmed;
        }

        return null;
    }
}
