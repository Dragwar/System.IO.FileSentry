using System;
using System.IO;

namespace ConsoleApp.Net452
{
    public sealed class Program
    {
        private static void Main(string[] args)
        {
            _ = args;
            using (var fsw = new FileSentry(@"C:\temp", "*.txt"))
            {
                fsw.Created += Fsw_Created;
                fsw.Created += delegate { };
                fsw.Created += (sender, e) => { };

                fsw.Changed += Fsw_Changed;
                fsw.Changed += delegate { };
                fsw.Changed += (sender, e) => { };

                fsw.Deleted += Fsw_Deleted;
                fsw.Deleted += delegate { };
                fsw.Deleted += (sender, e) => { };

                fsw.Renamed += Fsw_Renamed;
                fsw.Renamed += delegate { };
                fsw.Renamed += (sender, e) => { };

                fsw.EnableRaisingEvents = true;

                Console.WriteLine("Waiting for things to happen in C:\\temp");
                Console.ReadLine();
            }
        }

        private static void Fsw_Renamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine("Renamed: FileName - {0}, ChangeType - {1}, Old FileName - {2}", e.Name, e.ChangeType, e.OldName);
        }

        private static void Fsw_Deleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("Deleted: FileName - {0}, ChangeType - {1}", e.Name, e.ChangeType);
        }

        private static void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("Changed: FileName - {0}, ChangeType - {1}", e.Name, e.ChangeType);
        }

        private static void Fsw_Created(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("Created: FileName - {0}, ChangeType - {1}", e.Name, e.ChangeType);
        }
    }
}