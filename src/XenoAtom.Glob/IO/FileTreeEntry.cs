// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.IO;

/// <summary>
/// Represents one file system entry produced by <see cref="FileTreeWalker"/>.
/// </summary>
public readonly record struct FileTreeEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeEntry"/> struct.
    /// </summary>
    /// <param name="relativePath">The normalized relative path of the entry.</param>
    /// <param name="fullPath">The absolute full path of the entry.</param>
    /// <param name="name">The file or directory name of the entry.</param>
    /// <param name="isDirectory">A value indicating whether the entry is a directory.</param>
    /// <param name="attributes">The file system attributes captured during enumeration.</param>
    /// <param name="length">The entry length captured during enumeration.</param>
    /// <param name="creationTimeUtc">The creation time captured during enumeration, expressed as UTC.</param>
    /// <param name="lastAccessTimeUtc">The last access time captured during enumeration, expressed as UTC.</param>
    /// <param name="lastWriteTimeUtc">The last write time captured during enumeration, expressed as UTC.</param>
    /// <param name="isHidden">A value indicating whether the entry is hidden.</param>
    public FileTreeEntry(
        string relativePath,
        string fullPath,
        string name,
        bool isDirectory,
        FileAttributes attributes,
        long length,
        DateTimeOffset creationTimeUtc,
        DateTimeOffset lastAccessTimeUtc,
        DateTimeOffset lastWriteTimeUtc,
        bool isHidden)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        Name = name;
        IsDirectory = isDirectory;
        Attributes = attributes;
        Length = length;
        CreationTimeUtc = creationTimeUtc;
        LastAccessTimeUtc = lastAccessTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
        IsHidden = isHidden;
    }

    /// <summary>
    /// Gets the normalized relative path of the entry.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the absolute full path of the entry.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets the file or directory name of the entry.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the entry is a directory.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Gets the file system attributes captured during enumeration.
    /// </summary>
    public FileAttributes Attributes { get; }

    /// <summary>
    /// Gets the entry length captured during enumeration.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the creation time captured during enumeration, expressed as UTC.
    /// </summary>
    public DateTimeOffset CreationTimeUtc { get; }

    /// <summary>
    /// Gets the last access time captured during enumeration, expressed as UTC.
    /// </summary>
    public DateTimeOffset LastAccessTimeUtc { get; }

    /// <summary>
    /// Gets the last write time captured during enumeration, expressed as UTC.
    /// </summary>
    public DateTimeOffset LastWriteTimeUtc { get; }

    /// <summary>
    /// Gets a value indicating whether the entry is hidden.
    /// </summary>
    public bool IsHidden { get; }
}
