using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private const string SourcePath = @"D:\dev\git\DPG.Ecommerce";
        private const string ResourcesFile = @"D:\dev\git\DPG.Ecommerce\Source\DPG.Ecommerce.Resources\Resource-en-GB.json";
        private const string ResultsFileName = "results.csv";
        #endregion

        private static List<FilePath> _repositoryFiles;
        private static List<Resource> _resources;
        private static List<string> _metrics;

        private static StringBuilder _masterStringBuilder;
        private static string masterString;

        private static int ProcessedFiles { get; set; }

        public static void Main(string[] args)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            _repositoryFiles = new List<FilePath>();
            _resources = new List<Resource>();
            _masterStringBuilder = new StringBuilder();
            _metrics = new List<string>();

            LoadRepositoryFiles();
            Console.WriteLine($"Loaded {_repositoryFiles.Count} Repository files in {watch.ElapsedMilliseconds} ms.");
            LoadResources();
            Console.WriteLine($"Loaded {_resources.Count} Resources in {watch.ElapsedMilliseconds} ms.");

            CheckResources();

            Console.WriteLine("Checking files...");

            SaveResult();

            watch.Stop();

            Console.WriteLine($"Analyzed {_repositoryFiles.Count} files and {_resources.Count} resources in {watch.ElapsedMilliseconds} ms.");
            Console.WriteLine($"Unused resources list saved in {Directory.GetCurrentDirectory()}\\{ResultsFileName}");

            string metricsResult = String.Join(Environment.NewLine, _metrics.ToArray());
            File.WriteAllText("metrics.csv", metricsResult);

            Console.ReadLine();
        }

        /// <summary>
        /// Creates threads and divides source files between threads
        /// </summary>
        private static void CheckResources()
        {
            //Parallel.ForEach(_repositoryFiles.AsReadOnly(), ProcessFile);

            masterString = _masterStringBuilder.ToString().Replace(" ", string.Empty).Replace("\"", "'");

            

            Parallel.ForEach(_resources, ProcessResource);
        }

        private static void ProcessResource(Resource resource)
        {
            var foundCS = masterString.IndexOf($"{resource.Type}ResourceDictionary.{resource.Name}", StringComparison.OrdinalIgnoreCase) > -1;
            
            if (!foundCS)
            {
                
                var foundJS = masterString.IndexOf($"'{resource.Type}','{resource.Name}'", StringComparison.OrdinalIgnoreCase) > -1;

                if (foundJS)
                {
                    resource.Matches = 1;
                }
            }
            else
            {
                resource.Matches = 1;
            }

        }

        private static void SaveResult()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            StringBuilder sb = new StringBuilder();

            StreamWriter resultsfile;
            using (resultsfile = new StreamWriter(ResultsFileName))
            {
                var resourceList = _resources.Where(x => x.Matches == 0);

                foreach (var source in resourceList)
                {
                    sb.AppendLine($"{source.Type}, {source.Name}");
                }

                resultsfile.Write(sb.ToString());

                resultsfile.Close();

                watch.Stop();

                Console.WriteLine($"Saved {resourceList.Count()} unused resources in {watch.ElapsedMilliseconds} ms.");
            }
        }

        private static void ProcessFile(FilePath filesSource)
        {
            if (filesSource != null)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                var file = filesSource.Content;

                //var resourceList = _resources.Where(x => x.Matches == 0);

                foreach (var resource in _resources)
                {
                    if (resource.Matches == 0)
                    {
                        var matches = 0;

                        int indexType = file.IndexOf(resource.Type, StringComparison.OrdinalIgnoreCase);

                        if (indexType > 0)
                        {
                            int indexName = -1;

                            indexName = file.IndexOf(resource.Name, indexType, StringComparison.OrdinalIgnoreCase);

                            if ((indexType != -1 && indexName != -1) && (indexName - indexType < 5))
                            {
                                int startIndexString = indexType;
                                int endIndexString = indexType + resource.Name.Length + 10;

                                if (endIndexString > file.Length)
                                    endIndexString = file.Length;

                                string minorString = file.Substring(startIndexString, endIndexString - startIndexString);

                                if (!filesSource.IsJavascript)
                                {
                                    //var defaultRegex = new Regex($"{resource.Type}ResourceDictionary.{resource.Name}", RegexOptions.IgnoreCase);
                                    //matches = defaultRegex.Matches(file).Count;

                                    //matches = file.IndexOf($"{resource.Type}ResourceDictionary.{resource.Name}", StringComparison.OrdinalIgnoreCase) > -1 ? 1 : 0;

                                    matches = minorString.IndexOf($"{resource.Type}ResourceDictionary.{resource.Name}", StringComparison.OrdinalIgnoreCase) > -1 ? 1 : 0;
                                }
                                else if (filesSource.IsJavascript)
                                {
                                    var jsRegex = new Regex($"([\"\']){resource.Type}([\"\'])(^|, ?)([\"\']){resource.Name}([\"\'])", RegexOptions.IgnoreCase);
                                    //matches = jsRegex.Matches(file).Count > 0 ? 1 : 0;
                                    matches = jsRegex.Matches(minorString).Count > 0 ? 1 : 0;




                                    //matches = (
                                    //    file.IndexOf($"\"{resource.Type}\", \"{resource.Name}\"", StringComparison.OrdinalIgnoreCase) > -1 ||
                                    //    file.IndexOf($"\"{resource.Type}\",\"{resource.Name}\"", StringComparison.OrdinalIgnoreCase) > -1 ||
                                    //    file.IndexOf($"'{resource.Type}', '{resource.Name}'", StringComparison.OrdinalIgnoreCase) > -1 ||
                                    //    file.IndexOf($"'{resource.Type}','{resource.Name}'", StringComparison.OrdinalIgnoreCase) > -1
                                    //    ) ? 1 : 0;
                                }
                            }
                        }

                        resource.Matches += matches;
                    }
                }

                ProcessedFiles++;

                _metrics.Add($"{filesSource.Path}, {watch.ElapsedMilliseconds}");
            }
        }

        private static void LoadRepositoryFiles()
        {
            if (Directory.Exists(SourcePath))
            {
                ProcessRepository(SourcePath);
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
            //var filePathList = new List<FilePath>();

            using (var repo = new Repository(repositoryPath))
            {
                //RecursivelyGetPaths(filePathList, repo.Head.Tip.Tree);
                RecursivelyGetPaths(repo.Head.Tip.Tree);
            }

            //  Parallel.ForEach(filePathList, LoadFileMetadata);
        }

        private static void RecursivelyGetPaths(
            //List<FilePath> paths, 
            Tree tree)
        {
            foreach (TreeEntry te in tree)
            {
                string filePath = te.Path;

                if (!filePath.EndsWith(".generated.cs") && !filePath.EndsWith(".Designer.cs") && (filePath.EndsWith(".cs") || filePath.EndsWith(".cshtml") || filePath.EndsWith(".js")))
                {
                    string fullPath = $"{SourcePath}\\{te.Path}";
                    
                    string fileContent = File.ReadAllText(fullPath);

                    _masterStringBuilder.Append(fileContent);
                    
                    //_repositoryFiles.Add(new FilePath()
                    //{
                    //    Path = fullPath,
                    //    IsJavascript = filePath.EndsWith(".js"),
                    //    Content = File.ReadAllText(fullPath)
                    //});
                }

                if (te.TargetType == TreeEntryTargetType.Tree)
                {
                    //RecursivelyGetPaths(paths, te.Target as Tree);
                    RecursivelyGetPaths(te.Target as Tree);
                }
            }
        }

        //private static void LoadFileMetadata(string filePath)
        //{
        //    if (!filePath.EndsWith(".generated.cs") && (filePath.EndsWith(".cs") || filePath.EndsWith(".cshtml") || filePath.EndsWith(".js")))
        //    {
        //        _repositoryFiles.Add(new FilePath()
        //        {
        //            Path = filePath,
        //            IsJavascript = filePath.EndsWith(".js")
        //            ,
        //            Content = File.ReadAllText(filePath)
        //        });
        //    }
        //}
    }
}