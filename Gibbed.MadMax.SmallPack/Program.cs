/* Copyright (c) 2015 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Gibbed.IO;
using Gibbed.MadMax.FileFormats;
using NDesk.Options;

namespace Gibbed.MadMax.SmallUnpack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return null;
            //return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
        }

       

        public static void Main(string[] args)
        {
            bool verbose = false;
            bool overwriteFiles = false;
            bool listing = false;
            bool useFullPaths = true;
            bool showHelp = false;

            var endian = Endian.Little;

            var options = new OptionSet()
            {
                { "v|verbose", "be verbose (list files)", v => verbose = v != null },
                { "l|list", "just list files (don't extract)", v => listing = v != null },
                { "o|overwrite", "overwrite files if they already exist", v => overwriteFiles = v != null },
                { "f|full-path", "use full paths", v => useFullPaths = v != null },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extra.Count < 1 || extra.Count > 2 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_sarc [output_directory]", GetExecutableName());
                Console.WriteLine("pack specified small archive.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }


            //extra.Add("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mad Max\\dropzone\\locations\\a01\\key_locations\\enc1010\\a01_enc1010");

            string inputPath = extra[0];
            string baseOutputPath = extra.Count > 1
                                        ? extra[1]
                                        : inputPath + ".bl";

            var files = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);

            var pendingEntries = new List<SmallArchiveFile.PendingEntry>();

            foreach (var file in files)
            {
                pendingEntries.Add(new SmallArchiveFile.PendingEntry()
                {
                    Name = Path.GetRelativePath(inputPath, file).Replace("\\", "/"),
                    Path = file,
                });
            }

            using (var output = File.Create(baseOutputPath))
            {
                var headerSize = SmallArchiveFile.EstimateHeaderSize(pendingEntries);

                var smallArchive = new SmallArchiveFile();

                output.Position = headerSize;
                foreach (var pendingEntry in pendingEntries)
                {
                    if (pendingEntry.Size != null)
                    {
                        smallArchive.Entries.Add(new SmallArchiveFile.Entry(pendingEntry.Name,
                                                                            0,
                                                                            pendingEntry.Size.Value));
                        continue;
                    }

                    using (var input = File.OpenRead(pendingEntry.Path))
                    {
                        output.Position = output.Position.Align(4);
                        smallArchive.Entries.Add(new SmallArchiveFile.Entry(pendingEntry.Name,
                                                                            (uint)output.Position,
                                                                            (uint)input.Length));
                        output.WriteFromStream(input, input.Length);
                    }
                }

                output.Position = 0;
                smallArchive.Endian = endian;
                smallArchive.Serialize(output);
            }

            
        }
    }
}
