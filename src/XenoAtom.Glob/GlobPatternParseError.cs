// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob;

/// <summary>
/// Represents an error that occurred while parsing a glob pattern.
/// </summary>
public enum GlobPatternParseError
{
    /// <summary>
    /// The pattern was parsed successfully.
    /// </summary>
    None,

    /// <summary>
    /// The pattern contains a leading separator that is not allowed by the parser mode.
    /// </summary>
    LeadingSeparatorNotAllowed,

    /// <summary>
    /// The pattern contains a trailing separator that is not allowed by the parser mode.
    /// </summary>
    TrailingSeparatorNotAllowed,

    /// <summary>
    /// The pattern ends with an invalid escape sequence.
    /// </summary>
    InvalidEscapeSequence,

    /// <summary>
    /// The pattern contains an unterminated character class.
    /// </summary>
    UnterminatedCharacterClass,

    /// <summary>
    /// The pattern contains an invalid character class range.
    /// </summary>
    InvalidCharacterClassRange,
}
