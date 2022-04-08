using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using CliWrap;
using CliWrap.Buffered;
using WindowsInput;
using WindowsInput.Native;

namespace HotReload 
{
    public class Program
    {
        public static SemaphoreSlim BuildSema = new SemaphoreSlim(1);



        public static async void HandleChange(object sender, System.IO.FileSystemEventArgs args)
        {
            Task.Run(async () =>
            {
                try
                {
                    await BuildSema.WaitAsync(100);
                }
                catch(Exception ex)
                {
                    return;
                }
                try
                {
                    Console.WriteLine($@"{args.ChangeType} {args.Name}");
                    Console.WriteLine($@"Building new version...");
                    var result = await Cli.Wrap("pdc")
                        .WithArguments("source out")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync();
                    if (result.ExitCode != 0)
                    {
                        Console.WriteLine($@"Error building.");
                        Console.WriteLine(result.StandardOutput);
                        Console.WriteLine(result.StandardError);
                        return;
                    }
                    Console.WriteLine($@"Build complete!");
                    Console.WriteLine(result.StandardOutput);
                    Console.WriteLine(result.StandardError);
                    Console.WriteLine($@"Loading into sim...");
                    Console.WriteLine("\t\t\t\tWould call it here.");
                    BuildSema.Release();
                    Console.WriteLine($@"Done!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($@"Error building.\n{ex.ToString()}");
                    BuildSema.Release();
                }
            });
        }



        public static int Main(string[] args)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = "source";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Attributes;
            watcher.Filter = "*.*";
            watcher.Changed += HandleChange;
            watcher.Created += HandleChange;
            watcher.Deleted += HandleChange;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            Console.WriteLine("Watching for changes.");

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

            return 0;
        }
    }
}