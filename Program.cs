﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Siemens.Engineering;
using System.Threading;
using System.Configuration;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace AsyncArchiveTIA
{
    class Program
    {
        private static string plc;
        private static string hmi;



        static async Task Main(string[] args)
        {
            Console.SetWindowSize(60, 10);
            Console.Title = "AsyncArchiveTIA";
            //Subscribe to Assembly resolve event. Event fires when any assembly binding fails.
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            //Path to registry is HKEY_LOCAL_MACHINE\SOFTWARE\Siemens\Automation\Openness\*version\PublicAPI\*assembly
            List<string> versions = RegistryReader.GetVersions();

            //I always have only one version installed on my VMs so i can keep this fixed, you could read the versions list to retrieve other installed versions and choose which one you want
            List<string> assemblies = RegistryReader.GetAssemblies(versions[0]);
            RegistryReader.GetAssemblyPath(versions[0], assemblies.Last(), out plc, out hmi);

            List<string> projects = FindProjects();

            if (projects != null)
            {
                IEnumerable<Task> tasks = AsyncTaskList(projects);
                await Task.WhenAll(tasks);
                Console.WriteLine("All projects archived, closing TIA....");
            }
                    
            Thread.Sleep(3000);
        }

        /// <summary>
        /// Returns Project list found 
        /// </summary>
        /// <returns></returns>
        static List<string> FindProjects()
        {
            List<string> versions = new List<string>() { ".ap13", ".ap14", ".ap15", ".ap15_1", ".ap16", ".ap17" };
            List<string> targetDirectories = new List<string>();

            //If not custompath set, take Desktop
            if (RetrieveCustomPath() == null) targetDirectories.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            else targetDirectories = RetrieveCustomPath();

            List<string> projects = new List<string>();
            Int16 count = 0;

            foreach (var tDir in targetDirectories)
            {
                Console.WriteLine($"Searching projects in {tDir}");
                foreach (var dir in Directory.EnumerateDirectories(tDir))
                {
                    try
                    {
                        List<string> files = new List<string>(Directory.EnumerateFiles(dir));
                        foreach (var file in files)
                        {
                            if (versions.Contains(Path.GetExtension(file)))
                            {
                                projects.Add(Path.GetFullPath(file));
                                count++;
                            }

                        }
                    }
                    catch (Exception e)
                    {

                    }
                }
            }

            if (count > 1) Console.WriteLine($"{count} projects found");
            else if (count == 1) Console.WriteLine($"{count} project found");
            else Console.WriteLine("Couldn't find any project");

            if (projects.Any()) return projects;
            else return null;

        }

        /// <summary>
        /// If custom folder exists under MyDocuments folder, return the list of Folders where the projects we want to read archive
        /// </summary>
        /// <returns></returns>
        static List<string> RetrieveCustomPath()
        {
            List<string> customPaths = new List<string>();

            try
            {
                string[] customFile = File.ReadAllLines($"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\paths.txt");
                foreach (var path in customFile)
                {
                    customPaths.Add(path);
                }

                return customPaths;

            }

            catch
            {
                return null;
            }
        }

        static IEnumerable<Task> AsyncTaskList(List<string> projects)
        {

            foreach (string project in projects)
            {
                yield return ArchiveProject(project);
            }

        }

        static async Task ArchiveProject(string project)
        {
            await Task.Yield();
            using (TiaPortal tia = new TiaPortal(TiaPortalMode.WithoutUserInterface))
            {
                ProjectComposition tiaProjects = tia.Projects;
                string projectName = Path.GetFileNameWithoutExtension(project);
                DateTime moment = DateTime.Now;
                string date = $"{moment.Year}{moment.Month}{moment.Day}_{moment.Hour}{moment.Minute}";
                projectName = projectName + "_" + date;
                string projectExtension = Path.GetExtension(project);
                projectName = projectName + projectExtension.Insert(1, "z");

                Project tiaProject = null;
                FileInfo directorio = new FileInfo(project);
                DirectoryInfo target = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

                try
                {

                    Console.WriteLine($"Archiving project {Path.GetFileNameWithoutExtension(project)}");
                    tiaProject = tiaProjects.Open(directorio);
                    tiaProject.Archive(target, $"{projectName}", ProjectArchivationMode.Compressed);
                    Console.WriteLine($"{projectName} archived!");
                }

                catch (Exception e)
                {
                    Console.WriteLine($"Error archiving project {project}");
                    Console.WriteLine(e);
                    Console.ReadKey();
                }
                finally
                {
                    tiaProject.Close();
                    Console.WriteLine($"Closing project {project}");
                }
            }
            
        }


        public static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            string path = "";
            //Check for referenced Siemens assemblies.
            if (assemblyName.Name.EndsWith("Siemens.Engineering"))
                path = plc;
            if (assemblyName.Name.EndsWith("Siemens.Engineering.Hmi"))
                path = hmi;
            //Load Selected Siemens Assembly.
            if (string.IsNullOrEmpty(path) == false
                && File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }
            // return null for all other assemblies.
            return null;
        }
    }
}

