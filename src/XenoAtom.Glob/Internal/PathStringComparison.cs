// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct PathStringComparison(StringComparison Value)
{
    public static PathStringComparison Ordinal => new(StringComparison.Ordinal);

    public static PathStringComparison OrdinalIgnoreCase => new(StringComparison.OrdinalIgnoreCase);

    public static PathStringComparison CurrentPlatformDefault =>
        OperatingSystem.IsWindows() ? OrdinalIgnoreCase : Ordinal;

    public bool StartsWith(string left, string right) => left.StartsWith(right, Value);

    public bool StartsWith(ReadOnlySpan<char> left, ReadOnlySpan<char> right) => left.StartsWith(right, Value);

    public bool EndsWith(string left, string right) => left.EndsWith(right, Value);

    public bool EndsWith(ReadOnlySpan<char> left, ReadOnlySpan<char> right) => left.EndsWith(right, Value);

    public bool Equals(string left, string right) => string.Equals(left, right, Value);

    public bool Equals(ReadOnlySpan<char> left, ReadOnlySpan<char> right) => left.Equals(right, Value);
}
