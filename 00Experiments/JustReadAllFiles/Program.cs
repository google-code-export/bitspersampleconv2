using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace JustReadAllFiles {
    class Program {
        const int BUFF_BYTES = 1048576;

        static void ReadAllFilesOnDirectory(DirectoryInfo di) {
            var fArray = di.GetFiles();

            Parallel.For(0, fArray.Length, i => {
                try {
                    var f = fArray[i];
                    var fs = f.OpenRead();
                    int bytes = 0;
                    byte[] b = new byte[BUFF_BYTES];
                    do {
                        bytes = fs.Read(b, 0, BUFF_BYTES);
                    } while (bytes == BUFF_BYTES);
                    //Console.WriteLine("    {0}", f.Name);
                } catch (Exception ex) {
                    //Console.WriteLine("ReadAllFilesOnDirectory {0}", ex);
                }
            });
        }

        static void DirectoryScanRecursive(DirectoryInfo di) {
            ReadAllFilesOnDirectory(di);

            var dArray = di.GetDirectories();
            Parallel.For(0, dArray.Length, i => {
                try {
                    var subd = dArray[i];
                    Console.WriteLine("  {0}", subd.FullName);
                    DirectoryScanRecursive(subd);
                } catch (Exception ex) {
                    //Console.WriteLine("DirectoryScanRecursive {0}", ex);
                }
            });
        }

        static void Main(string[] args) {
            string path = "C:\\";
            if (1 <= args.Length) {
                path = args[0];
            }

            DirectoryInfo di = null;

            try {
                di = new DirectoryInfo(path);
            } catch (Exception ex) {
                Console.WriteLine("Usage: This program accepts 1 argument: directory_path_to_scan");
                return;
            }

            DirectoryScanRecursive(di);
        }
    }
}
