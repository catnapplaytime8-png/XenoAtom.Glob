// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct PathNormalizationResult(bool Success, NormalizedPath Path, PathNormalizationError Error)
{
    public static PathNormalizationResult FromPath(NormalizedPath path) => new(true, path, PathNormalizationError.None);

    public static PathNormalizationResult Failure(PathNormalizationError error) => new(false, default, error);
}
