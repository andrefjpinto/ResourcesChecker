using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ResourcesChecker.Models;

namespace ResourcesChecker
{
    public class BetterSolution
    {
        private readonly IEnumerable<SourceFile> _sourcesFiles;
        private readonly IEnumerable<Resource> _resources;

        public BetterSolution(IEnumerable<SourceFile> sourcesFiles, IEnumerable<Resource> resources)
        {
            _resources = resources;
            _sourcesFiles = sourcesFiles;
        }

        public void Run()
        {
            var sourceFilesList = _sourcesFiles.Where(x => !x.IsJs).ToArray();
            Parallel.ForEach(sourceFilesList, Execute);

            var sourceFilesListJs = _sourcesFiles.Where(x => x.IsJs).ToArray();
            Parallel.ForEach(sourceFilesListJs, ExecuteJs);
        }

        private void Execute(SourceFile file)
        {
            foreach (var resource in _resources.Where(x => x.Matches == 0 && file.Source.IndexOf($"{x.Type}ResourceDictionary.{x.Name}", StringComparison.OrdinalIgnoreCase) >= 0))
                resource.Matches = 1;
        }

        private void ExecuteJs(SourceFile file)
        {
            foreach (var resource in _resources.Where(x => x.Matches == 0 && file.Source.Contains($"\"{x.Type}\",\"{x.Name}\"")))
                resource.Matches = 1;
        }
    }
}
