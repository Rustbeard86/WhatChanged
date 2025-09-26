// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly:
    SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member",
        Target =
            "~M:WhatChanged.Core.Services.ArchiveService.CreateUpdateArchive(System.IO.DirectoryInfo,WhatChanged.Core.Models.ChangeReport)~System.String")]
[assembly:
    SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member",
        Target =
            "~M:WhatChanged.Core.Services.ComparisonService.GenerateChangeReport(System.Collections.Generic.IReadOnlyDictionary{System.String,WhatChanged.Core.Models.FileSystemEntry},System.Collections.Generic.IReadOnlyDictionary{System.String,WhatChanged.Core.Models.FileSystemEntry})~WhatChanged.Core.Models.ChangeReport")]