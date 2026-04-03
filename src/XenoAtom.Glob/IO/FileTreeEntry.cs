// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.IO;

/// <summary>
/// Represents one file system entry produced by <see cref="FileTreeWalker"/>.
/// </summary>
public readonly record struct FileTreeEntry(string RelativePath, string FullPath, bool IsDirectory);
