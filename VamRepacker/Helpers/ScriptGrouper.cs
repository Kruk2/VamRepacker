﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using VamRepacker.Logging;
using VamRepacker.Models;

namespace VamRepacker.Helpers;

public interface IScriptGrouper
{
    Task GroupCslistRefs<T>(List<T> files, Func<string, Stream> openFileStream) where T : FileReferenceBase;
}

public class ScriptGrouper : IScriptGrouper
{
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    public ScriptGrouper(IFileSystem fs, ILogger logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task GroupCslistRefs<T>(List<T> files, Func<string, Stream> openFileStream) where T: FileReferenceBase
    {
        var filesMovedAsChildren = new HashSet<T>();
        var filesIndex = files
            .Where(f => f.ExtLower == ".cs")
            .ToDictionary(f => f.LocalPath);
        foreach (var cslist in files.Where(f => f.ExtLower == ".cslist"))
        {
            var cslistFolder = _fs.Path.GetDirectoryName(cslist.LocalPath);
            using var streamReader = new StreamReader(openFileStream(cslist.LocalPath));

            while (!streamReader.EndOfStream)
            {
                var cslistRef = (await streamReader.ReadLineAsync())?.Trim();
                if (string.IsNullOrWhiteSpace(cslistRef)) continue;
                if (filesIndex.TryGetValue(_fs.Path.Combine(cslistFolder, cslistRef).NormalizePathSeparators(), out var f1))
                {
                    cslist.AddChildren(f1);
                    filesMovedAsChildren.Add(f1);
                }
                else
                {
                    cslist.AddMissingChildren(cslistRef);
                    //if(cslist is VarPackageFile varFile)
                    //    _logger.Log($"[MISSING-SCRIPT] {cslistRef} in {cslist} in {varFile.ParentVar.Path}");
                    //else
                    //    _logger.Log($"[MISSING-SCRIPT] {cslistRef} in {cslist}");
                }
            }
        }

        files.RemoveAll(t => filesMovedAsChildren.Contains(t));
    }
}