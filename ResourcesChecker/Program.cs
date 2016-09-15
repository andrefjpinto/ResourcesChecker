using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MAB.DotIgnore;
using Newtonsoft.Json;
using ResourcesChecker.Models;

namespace ResourcesChecker
{
    public static class Program
    {
        #region Configuration
        private const string SourcePath = @"C:\dev\git\DPG.Ecommerce";
        private const string IgnoreFile = @"C:\dev\git\DPG.Ecommerce\.gitignore";
        private const string ResourcesFile = @"C:\dev\git\DPG.Ecommerce\Source\DPG.Ecommerce.Resources\Resource-en-GB.json";
        private const string ResultsFileName = "results.csv";
        private const int NumberOfThreads = 4;
        #endregion

        private static List<string> _sourcesFiles;
        private static IgnoreList _ignores;
        private static List<Resource> _resources;
        private static Dictionary<string, int> _threadsInfo;
        private static List<Task> _threadList;

        private static int ProcessedFiles { get; set; }

        public static void Main(string[] args)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            _ignores = new IgnoreList(IgnoreFile);
            _sourcesFiles = new List<string>();
            _resources = new List<Resource>();
            _threadsInfo = new Dictionary<string, int>();
            _threadList = new List<Task>();

            LoadSourceFiles();
            LoadResources();
            
            CheckResources();

            Console.WriteLine("Checking files...");

            Task.WaitAll(_threadList.ToArray());

            SaveResult();

            watch.Stop();

            Console.WriteLine($"Analyzed {_sourcesFiles.Count} files and {_resources.Count} resources in {watch.ElapsedMilliseconds} ms.");
            Console.WriteLine($"Unused resources list saved in {Directory.GetCurrentDirectory()}\\{ResultsFileName}");
        }

        /// <summary>
        /// Creates threads and divides source files between threads
        /// </summary>
        /// <param name="auxStart"></param>
        private static void CheckResources()
        {
            var auxStart = 0;

            _threadsInfo.Add("thread0", _sourcesFiles.Count % NumberOfThreads + _sourcesFiles.Count / NumberOfThreads);

            for (var i = 1; i < NumberOfThreads; i++)
            {
                _threadsInfo.Add($"thread{i}", _sourcesFiles.Count / NumberOfThreads);
            }

            foreach (var threadInfo in _threadsInfo)
            {
                if (auxStart < _sourcesFiles.Count)
                {
                    var sdfsd = auxStart;

                    _threadList.Add(
                        Task.Factory.StartNew(
                            () => FindUnusedResources(_sourcesFiles.GetRange(sdfsd, threadInfo.Value))));
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

        private static void FindUnusedResources(IEnumerable<string> sourceFiles)
        {
            foreach (var filesSource in sourceFiles)
            {
                var file = File.ReadAllText(filesSource);

                foreach (var resource in _resources.Where(x => x.Matches == 0))
                {
                    var matches = 0;

                    if (filesSource.EndsWith(".cs") || filesSource.EndsWith(".cshtml"))
                    {
                        var defaultRegex = new Regex($"{resource.Type}ResourceDictionary.{resource.Name}", RegexOptions.IgnoreCase);
                        matches = defaultRegex.Matches(file).Count;
                    }
                    else if (filesSource.EndsWith(".js"))
                    {
                        var jsRegex = new Regex($"([\"\']){resource.Type}([\"\'])(^|, ?)([\"\']){resource.Name}([\"\'])", RegexOptions.IgnoreCase);
                        matches = jsRegex.Matches(file).Count;
                    }

                    resource.Matches += matches;
                }

                ProcessedFiles++;
                //Console.WriteLine($"Processed Files: {ProcessedFiles} of {_sourcesFiles.Count}. Thread ID: {Thread.CurrentThread.ManagedThreadId}");
            }
        }

        private static void LoadSourceFiles()
        {
            if (Directory.Exists(SourcePath))
            {
                ProcessDirectory(SourcePath);
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
        private static void ProcessDirectory(string targetDirectory)
        {
            // Process the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);
            foreach (string fileName in fileEntries)
                ProcessFile(fileName);

            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);

            // Validates if directory isn't in the ignore list
            foreach (string subdirectory in subdirectoryEntries)
            {
                if (!_ignores.IsIgnored(new DirectoryInfo(subdirectory)))
                    ProcessDirectory(subdirectory);
            }
        }

        private static void ProcessFile(string path)
        {
            if (!path.EndsWith(".generated.cs") && (path.EndsWith(".cs") || path.EndsWith(".cshtml") || path.EndsWith(".js")))
            {
                _sourcesFiles.Add(path);
            }
        }
    }
}