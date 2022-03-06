using System;
using System.Collections.Generic;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public class VarPackage : IVamObjectWithDependencies
    {
        public VarPackageName Name { get; }
        public long Size { get; set; }
        public string FullPath { get; }
        public bool IsInVaMDir { get; }
        public List<VarPackageFile> Files { get; }
        public List<JsonFile> JsonFiles { get; } = new();

        public List<VarPackage> TrimmedResolvedVarDependencies { get; private set; }
        public List<VarPackage> AllResolvedVarDependencies { get; private set; }
        public List<FreeFile> TrimmedResolvedFreeDependencies { get; private set; }
        public List<FreeFile> AllResolvedFreeDependencies { get; private set; }

        public IEnumerable<string> UnresolvedDependencies => JsonFiles
            .SelectMany(t => t.Missing.Select(x => x.Value + " from " + t))
            .Distinct();

        private Dictionary<string, VarPackageFile> _filesDict;

        public Dictionary<string, VarPackageFile> FilesDict => _filesDict ??= Files
            .SelectMany(t => t.SelfAndChildren())
            .GroupBy(t => t.LocalPath, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(t => t.Key, t => t.First(), StringComparer.InvariantCultureIgnoreCase);

        public bool AlreadyCalculatedDeps => AllResolvedFreeDependencies != null;

        private bool _dirty;
        public bool Dirty
        {
            get => _dirty;
            set
            {
                _dirty = value;
                Files.ForEach(t => t.Dirty = value);
            }
        }

        public VarPackage(
            VarPackageName name, 
            string fullPath, 
            List<VarPackageFile> files,
            bool isInVamDir,
            long size)
        {
            Name = name;
            FullPath = fullPath.NormalizePathSeparators();
            Files = files;
            IsInVaMDir = isInVamDir;
            Size = size;
        }

        public override string ToString() => Name.ToString();

        public void CalculateDeps()
        {
            if (AlreadyCalculatedDeps) return;
            (AllResolvedVarDependencies, AllResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(JsonFiles);
        }

        public void CalculateShallowDeps()
        {
            if(TrimmedResolvedFreeDependencies != null) return;
            (TrimmedResolvedVarDependencies, TrimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
        }

        public void ClearDependencies()
        {
            AllResolvedFreeDependencies = null;
            AllResolvedVarDependencies = null;
            TrimmedResolvedFreeDependencies = null;
            TrimmedResolvedVarDependencies = null;
        }
    }
}