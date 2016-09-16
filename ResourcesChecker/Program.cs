using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Newtonsoft.Json;
using ResourcesChecker.Extensions;
using ResourcesChecker.Models;

namespace ResourcesChecker
{
    public static class Program
    {
        #region Configuration
        private const string SourcePath = @"c:\dev\git\DPG.Ecommerce";
        private const string ResourcesFile = @"c:\dev\git\DPG.Ecommerce\Source\DPG.Ecommerce.Resources\Resource-en-GB.json";
        private const string ResultsFileName = "results.csv";
        #endregion

        private static Resource[] _resources;
        private static List<SourceFile> _sourceFiles;

        public static void Main(string[] args)
        {
            var watch = Stopwatch.StartNew();
            _sourceFiles = new List<SourceFile>();

            LoadRepositoryFiles();
            LoadResources();

            Console.WriteLine("Checking files...");
            new BetterSolution(_sourceFiles, _resources).Run();

            SaveResult();
            watch.Stop();

            Console.WriteLine($"Analyzed {_sourceFiles.Count} files and {_resources.Length} resources in {watch.ElapsedMilliseconds} ms.");
            Console.WriteLine($"Unused resources list saved in {Directory.GetCurrentDirectory()}\\{ResultsFileName}");
            Console.ReadLine();
        }

        private static void SaveResult()
        {
            File.WriteAllLines(ResultsFileName, _resources.Where(x => x.Matches == 0).Select(x => x.Type + ", " + x.Name));
        }

        private static void LoadRepositoryFiles()
        {
            if (Directory.Exists(SourcePath))
                ProcessRepository(SourcePath);

            Console.WriteLine("Resources:\t Loaded");
        }

        private static void LoadResources()
        {
            var jsonFile = File.ReadAllText(ResourcesFile);
            _resources = JsonConvert.DeserializeObject<Resource[]>(jsonFile);

            Console.WriteLine("Source Files:\t Loaded");
        }

        private static void ProcessRepository(string repositoryPath)
        {
            using (var repo = new Repository(repositoryPath))
            {
                RecursivelyGetPaths(repo.Head.Tip.Tree);
            }
        }

        private static void RecursivelyGetPaths(Tree tree)
        {
            foreach (var te in tree)
            {
                if (te.Path.IsJs())
                {
                    _sourceFiles.Add(new SourceFile
                    {
                        Source = File.ReadAllText(Path.Combine(SourcePath, te.Path))
                        .Replace(" ", string.Empty).Replace("'", "\""),
                        IsJs = true
                    });
                }
                else if (te.Path.IsCshtml() || te.Path.IsCs())
                {
                    _sourceFiles.Add(new SourceFile { Source = File.ReadAllText(Path.Combine(SourcePath, te.Path)) });
                }

                if (te.TargetType == TreeEntryTargetType.Tree)
                    RecursivelyGetPaths(te.Target as Tree);
            }
        }
    }
}