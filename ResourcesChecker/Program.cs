using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LibGit2Sharp;
using MAB.DotIgnore;
using Newtonsoft.Json;
using ResourcesChecker.Models;

namespace ResourcesChecker
{
    public static class Program
    {
        #region Configuration
        private const string SourcePath = @"z:\dev\git\DPG.Ecommerce";
        private const string ResourcesFile = @"z:\dev\git\DPG.Ecommerce\Source\DPG.Ecommerce.Resources\Resource-en-GB.json";
        private const string ResultsFileName = "results.csv";
        private const int NumberOfThreads = 8;
        #endregion

        private static List<FilePath> _repositoryFiles;
        private static List<Resource> _resources;
        private static Dictionary<string, int> _threadsInfo;
        private static List<Task> _threadList;

        private static int ProcessedFiles { get; set; }

        public static void Main(string[] args)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            _repositoryFiles = new List<FilePath>();
            _resources = new List<Resource>();
            _threadsInfo = new Dictionary<string, int>();
            _threadList = new List<Task>();

            LoadRepositoryFiles();
            Console.WriteLine($"Loaded {_repositoryFiles.Count} Repository files in {watch.ElapsedMilliseconds} ms.");
            LoadResources();
            Console.WriteLine($"Loaded {_resources.Count} Resources in {watch.ElapsedMilliseconds} ms.");

            CheckResources();

            Console.WriteLine("Checking files...");

            Task.WaitAll(_threadList.ToArray());

            SaveResult();

            watch.Stop();
            
            Console.WriteLine($"Analyzed {_repositoryFiles.Count} files and {_resources.Count} resources in {watch.ElapsedMilliseconds} ms.");
            Console.WriteLine($"Unused resources list saved in {Directory.GetCurrentDirectory()}\\{ResultsFileName}");
        }

        /// <summary>
        /// Creates threads and divides source files between threads
        /// </summary>
        private static void CheckResources()
        {
            var auxStart = 0;

            _threadsInfo.Add("thread0", _repositoryFiles.Count % NumberOfThreads + _repositoryFiles.Count / NumberOfThreads);

            for (var i = 1; i < NumberOfThreads; i++)
            {
                _threadsInfo.Add($"thread{i}", _repositoryFiles.Count / NumberOfThreads);
            }

            foreach (var threadInfo in _threadsInfo)
            {
                if (auxStart < _repositoryFiles.Count)
                {
                    var aux = auxStart;

                    _threadList.Add(
                        Task.Factory.StartNew(
                            () => FindUnusedResources(_repositoryFiles.GetRange(aux, threadInfo.Value))));
                }
                auxStart += threadInfo.Value;
            }
        }

        private static void SaveResult()
        {
            StreamWriter resultsfile;
            using (resultsfile = new StreamWriter(ResultsFileName))
            {
                foreach (var source in _resources.Where(x => x.Matches == 0))
                {
                    resultsfile.WriteLine(source.Type + ", " + source.Name);
                }

                resultsfile.Close();
            }
        }

        private static void FindUnusedResources(IEnumerable<FilePath> sourceFiles)
        {
            foreach (var filesSource in sourceFiles)
            {
                var file = filesSource.Content;

                foreach (var resource in _resources.Where(x => x.Matches == 0))
                {
                    var matches = 0;
                    if (!filesSource.IsJavascript)
                    {
                        var defaultRegex = new Regex($"{resource.Type}ResourceDictionary.{resource.Name}", RegexOptions.IgnoreCase);
                        matches = defaultRegex.Matches(file).Count;
                    }
                    else if (filesSource.IsJavascript)
                    {
                        var jsRegex = new Regex($"([\"\']){resource.Type}([\"\'])(^|, ?)([\"\']){resource.Name}([\"\'])", RegexOptions.IgnoreCase);
                        matches = jsRegex.Matches(file).Count;
                    }

                    resource.Matches += matches;
                }

                ProcessedFiles++;
            }
        }

        private static void LoadRepositoryFiles()
        {
            if (Directory.Exists(SourcePath))
            {
                //var watch = System.Diagnostics.Stopwatch.StartNew();

                ProcessRepository(SourcePath);

                //watch.Stop();

                //Console.WriteLine($"Repository {_sourcesFiles.Count} files and {_resources.Count} resources in {watch.ElapsedMilliseconds} ms.");
            }

            Console.WriteLine("Resources:\t Loaded");
        }

        private static void LoadResources()
        {
            using (var r = File.OpenText(ResourcesFile))
            {
                var json = r.ReadToEnd();
                _resources.AddRange(JsonConvert.DeserializeObject<IEnumerable<Resource>>(json));
            }

            Console.WriteLine("Source Files:\t Loaded");
        }


        // Process all files in the directory passed in, recurse on any directories 
        // that are found, and process the files they contain.
        private static void ProcessRepository(string repositoryPath)
        {
            using (var repo = new Repository(repositoryPath))
            {
                //List<String> paths = new List<string>();
                RecursivelyGetPaths(_repositoryFiles, repo.Head.Tip.Tree);
            }
        }

        private static void RecursivelyGetPaths(List<FilePath> paths, Tree tree)
        {
            foreach (TreeEntry te in tree)
            {
                if (!te.Path.EndsWith(".generated.cs") && (te.Path.EndsWith(".cs") || te.Path.EndsWith(".cshtml") || te.Path.EndsWith(".js")))
                {
                    
                        string filepath = $"{SourcePath}\\{te.Path}";
                        //Console.WriteLine($"Loading file: {filepath}");
                        
                        var fileContent = File.ReadAllText(filepath);

                        paths.Add(new FilePath()
                        {
                            Path = te.Path,
                            IsJavascript = te.Path.EndsWith(".js"),
                            Content = fileContent
                        });
                    
                }

                if (te.TargetType == TreeEntryTargetType.Tree)
                {
                    RecursivelyGetPaths(paths, te.Target as Tree);
                }
            }
        }
    }
}