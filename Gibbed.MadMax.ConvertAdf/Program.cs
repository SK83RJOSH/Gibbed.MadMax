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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Gibbed.IO;
using NDesk.Options;
using MemberDefinition = Gibbed.MadMax.FileFormats.AdfFile.MemberDefinition;
using TypeDefinition = Gibbed.MadMax.FileFormats.AdfFile.TypeDefinition;
using TypeDefinitionType = Gibbed.MadMax.FileFormats.AdfFile.TypeDefinitionType;

namespace Gibbed.MadMax.ConvertAdf
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static void SetOption<T>(string s, ref T variable, T value)
        {
            if (s == null)
            {
                return;
            }

            variable = value;
        }

        internal enum Mode
        {
            Unknown,
            Export,
            Import,
        }

        private static void Main(string[] args)
        {
            var mode = Mode.Unknown;
            bool showHelp = false;
            var typeLibraryPaths = new List<string>();

            var options = new OptionSet
            {
                // ReSharper disable AccessToModifiedClosure
                { "e|export", "convert from binary to XML", v => SetOption(v, ref mode, Mode.Export) },
                { "i|import", "convert from XML to binary", v => SetOption(v, ref mode, Mode.Import) },
                // ReSharper restore AccessToModifiedClosure
                { "t|type-library=", "load type library from file", v => typeLibraryPaths.Add(v) },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;

            //args = new string[1];
            //args[0] = "C:\\get_player_scrap.gsrc";//"C:\\mapicons.mapiconsc";
            //typeLibraryPaths.Add("C:\\gsrc.adf");
            //args[0] = "C:\\mapicons.mapiconsc";
            //args[0] = "C:\\map.guixc";
            //typeLibraryPaths.Add("C:\\guixc.adf");
            //args[0] = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mad Max\\archives_win64\\unpacked\\global\\location_info_unpack\\global\\location_info.locationinfoc";
            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (mode == Mode.Unknown && extras.Count >= 1)
            {
                var extension = Path.GetExtension(extras[0]);
                if (extension != null && extension.ToLowerInvariant() == ".xml")
                {
                    mode = Mode.Import;
                }
                else
                {
                    mode = Mode.Export;
                }
            }

            if (extras.Count < 1 || extras.Count > 2 ||
                showHelp == true ||
                mode == Mode.Unknown)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ [-e] input_adf [output_xml]", GetExecutableName());
                Console.WriteLine("       {0} [OPTIONS]+ [-i] input_xml [output_adf]", GetExecutableName());
                Console.WriteLine("Convert an ADF file between binary and XML format.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var runtime = new RuntimeTypeLibrary();
            foreach (var typeLibraryPath in typeLibraryPaths)
            {
                var adf = new FileFormats.AdfFile();

                using (var input = File.OpenRead(typeLibraryPath))
                {
                    adf.Deserialize(input);
                }

                if (adf.InstanceInfos.Count > 0)
                {
                    //throw new InvalidOperationException();
                }

                runtime.AddTypeDefinitions(adf);
            }

            if (mode == Mode.Export)
            {
                string inputPath = extras[0];
                string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, ".xml");

                var adf = new FileFormats.AdfFile();
                using (var input = File.OpenRead(inputPath))
                {
                    adf.Deserialize(input);
                    var endian = adf.Endian;

                    //if (adf.TypeDefinitions.Count > 0)
                    //{
                    //    throw new NotSupportedException();
                    //}

                    runtime.AddTypeDefinitions(adf);

                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "    ",
                        CheckCharacters = false,
                    };

                    using (var output = File.Create(outputPath))
                    {
                        var writer = XmlWriter.Create(output, settings);
                        writer.WriteStartDocument();
                        writer.WriteStartElement("adf");

                        if (adf.InstanceInfos.Count > 0)
                        {
                            writer.WriteStartElement("instances");

                            foreach (var instanceInfo in adf.InstanceInfos)
                            {
                                writer.WriteStartElement("instance");
                                Console.WriteLine(instanceInfo.Name);

                               // writer.WriteStartElement("root");
                                writer.WriteAttributeString("root", instanceInfo.Name);

                                var typeDefinition = runtime.GetTypeDefinition(instanceInfo.TypeHash);
                                input.Position = instanceInfo.Offset;
                                Console.WriteLine("TypeDef FilePos Data {0:X}", input.Position);
                                using (var data = input.ReadToMemoryStream((int)instanceInfo.Size))
                                {
                                    WriteInstance(typeDefinition, instanceInfo.Name, data, writer, endian, runtime);
                                }

                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                        writer.WriteEndDocument();
                        writer.Flush();
                    }
                }
            }
            else if (mode == Mode.Import)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private struct WorkItem
        {
            public long Id;
            public string Name;
            public TypeDefinition TypeDefinition;
            public long Offset;

            public WorkItem(long id, string name, TypeDefinition typeDefinition, long offset)
            {
                this.Id = id;
                this.Name = name;
                this.TypeDefinition = typeDefinition;
                this.Offset = offset;
            }
        }

        private static void WriteInstance(TypeDefinition rootTypeDefinition,
                                          string name,
                                          MemoryStream data,
                                          XmlWriter writer,
                                          Endian endian,
                                          RuntimeTypeLibrary runtime)
        {
            long counter = 0;
            var queue = new Queue<WorkItem>();
            queue.Enqueue(new WorkItem(counter++, name, rootTypeDefinition, 0));

            while (queue.Count > 0)
            {
                var workItem = queue.Dequeue();
                Console.WriteLine(workItem.Name);
                switch (workItem.TypeDefinition.Type)
                {
                    case TypeDefinitionType.Structure:
                    {
                        data.Position = workItem.Offset;
                        WriteStructure(
                            writer,
                            workItem.TypeDefinition,
                            workItem.Id,
                            workItem.Name,
                            data,
                            endian,
                            runtime,
                            ref counter,
                            queue);
                        break;
                    }

                    case TypeDefinitionType.Array:
                    {
                        data.Position = workItem.Offset;
                        WriteArray(
                            writer,
                            workItem.TypeDefinition,
                            workItem.Id,
                            data,
                            endian,
                            runtime,
                            ref counter,
                            queue);
                        break;
                    }

                    default:
                    {   
                            data.Position = workItem.Offset;
                        WriteArray(
                            writer,
                            workItem.TypeDefinition,
                            workItem.Id,
                            data,
                            endian,
                            runtime,
                            ref counter,
                            queue);
                            break;
                        //throw new NotImplementedException();
                    }
                }
            }
        }

        private static void WriteStructure(XmlWriter writer,
                                           TypeDefinition typeDefinition,
                                           long id,
                                           string name,
                                           MemoryStream data,
                                           Endian endian,
                                           RuntimeTypeLibrary runtime,
                                           ref long counter,
                                           Queue<WorkItem> queue)
        {
            var basePosition = data.Position;

            writer.WriteStartElement("struct");
            writer.WriteAttributeString("type", typeDefinition.Name);

            if (name != null)
            {
                writer.WriteAttributeString("name", name);
            }

            if (id >= 0)
            {
                writer.WriteAttributeString("id", "#" + id);
            }

            foreach (var memberDefinition in typeDefinition.Members)
            {
                data.Position = basePosition + memberDefinition.Offset;
                WriteMember(writer, data, endian, runtime, memberDefinition, ref counter, queue);
            }

            writer.WriteEndElement();
        }

        private static void WriteMember(XmlWriter writer,
                                        MemoryStream data,
                                        Endian endian,
                                        RuntimeTypeLibrary runtime,
                                        MemberDefinition memberDefinition,
                                        ref long counter,
                                        Queue<WorkItem> queue)
        {
            writer.WriteStartElement("member");
            writer.WriteAttributeString("name", memberDefinition.Name);

            switch (memberDefinition.TypeHash)
            {
                case TypeHashes.Primitive.UInt8:
                    {
                        var value = data.ReadValueU8();
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.Int8:
                    {
                        var value = data.ReadValueS8();
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.UInt16:
                {
                    var value = data.ReadValueU16(endian);
                    writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                case TypeHashes.Primitive.UInt32:
                {
                    var value = data.ReadValueU32(endian);
                    writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                case TypeHashes.Primitive.UInt64:
                {
                    var value = data.ReadValueU64(endian);
                    writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                case TypeHashes.Primitive.Int16:
                    {
                        var value = data.ReadValueS16(endian);
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.Int32:
                    {
                        var value = data.ReadValueS32(endian);
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.Int64:
                    {
                        var value = data.ReadValueS64(endian);
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.Float:
                    {
                        var value = data.ReadValueF32(endian);
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.Double:
                    {
                        var value = data.ReadValueF64(endian);
                        writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
                        break;
                    }

                case TypeHashes.Primitive.String:
                    {
                        var offset = data.ReadValueS64(endian);
                        data.Position = offset;
                        var value = data.ReadStringZ(Encoding.UTF8);
                        Console.WriteLine(value);
                        writer.WriteValue(value);
                        break;
                    }

                default:
                {
                    var typeDefinition = runtime.GetTypeDefinition(memberDefinition.TypeHash);
                    switch (typeDefinition.Type)
                    {
                        case TypeDefinitionType.Structure:
                        {
                            WriteStructure(writer, typeDefinition, -1, null, data, endian, runtime, ref counter, queue);
                            break;
                        }

                        case TypeDefinitionType.Array:
                        {
                            var id = counter++;
                            queue.Enqueue(new WorkItem(id, null, typeDefinition, data.Position));
                            writer.WriteValue("#" + id.ToString(CultureInfo.InvariantCulture));
                            break;
                        }

                            case TypeDefinitionType.InlineArray:
                                {
                                    WriteArrayItems(writer,
                                                         typeDefinition,
                                                         -1,
                                                         data, endian, runtime, ref counter, queue, data.Position, typeDefinition.ElementLength);
                                    break;
                                }

                            case TypeDefinitionType.Pointer:
                                {
                                    writer.WriteValue("POINTER");
                                    break;
                                }

                            case TypeDefinitionType.BitField:
                                {
                                    writer.WriteValue("BitField:UNK");
                                    break;
                                }

                            case TypeDefinitionType.Enumeration:
                                {
                                    var enumID = data.ReadValueU32(endian);
                                    writer.WriteValue(typeDefinition.Members[typeDefinition.membersEnum[enumID]].Name+":"+enumID);
                                    break;
                                }

                            default:
                            {
                                throw new NotSupportedException();
                            }
                    }

                    break;
                }
            }

            writer.WriteEndElement();
        }

        private static void WriteArray(XmlWriter writer,
                                       TypeDefinition typeDefinition,
                                       long id,
                                       MemoryStream data,
                                       Endian endian,
                                       RuntimeTypeLibrary runtime,
                                       ref long counter,
                                       Queue<WorkItem> queue)
        {
            var offset = data.ReadValueS64(endian);
            var count = data.ReadValueS64(endian);
            WriteArrayItems(writer,
                                 typeDefinition,
                                 id,
                                 data, endian, runtime, ref counter, queue,
                                 offset, count);
        }

        private static void WriteArrayItems(XmlWriter writer,
                                       TypeDefinition typeDefinition,
                                       long id,
                                       MemoryStream data,
                                       Endian endian,
                                       RuntimeTypeLibrary runtime,
                                       ref long counter,
                                       Queue<WorkItem> queue, long offset, long count)
        {
            writer.WriteStartElement("array");

            if (id >= 0)
            {
                writer.WriteAttributeString("id", "#" + id);
            }

            //Console.WriteLine("Write Array {0:X}", data.Position);

            switch (typeDefinition.ElementTypeHash)
            {
                case TypeHashes.Primitive.UInt8:
                {
                    data.Position = offset;
                    var sb = new StringBuilder();
                    for (long i = 0; i < count; i++)
                    {
                        var value = data.ReadValueU8();
                        sb.Append(value.ToString(CultureInfo.InvariantCulture));
                        sb.Append(" ");
                    }
                    writer.WriteValue(sb.ToString());
                    break;
                }

                case TypeHashes.Primitive.Int8:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueS8();
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                case TypeHashes.Primitive.UInt16:
                {
                    data.Position = offset;
                    var sb = new StringBuilder();
                    for (long i = 0; i < count; i++)
                    {
                        var value = data.ReadValueU16(endian);
                        sb.Append(value.ToString(CultureInfo.InvariantCulture));
                        sb.Append(" ");
                    }
                    writer.WriteValue(sb.ToString());
                    break;
                }

                case TypeHashes.Primitive.Int16:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueS16(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }


                case TypeHashes.Primitive.UInt32:
                {
                    data.Position = offset;
                    var sb = new StringBuilder();
                    for (long i = 0; i < count; i++)
                    {
                        var value = data.ReadValueU32(endian);
                        sb.Append(value.ToString(CultureInfo.InvariantCulture));
                        sb.Append(" ");
                    }
                    writer.WriteValue(sb.ToString());
                    break;
                }

                case TypeHashes.Primitive.Int32:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueS32(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                case TypeHashes.Primitive.UInt64:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueU64(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                case TypeHashes.Primitive.Int64:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueS64(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                case TypeHashes.Primitive.Float:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueF32(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                case TypeHashes.Primitive.Double:
                    {
                        data.Position = offset;
                        var sb = new StringBuilder();
                        for (long i = 0; i < count; i++)
                        {
                            var value = data.ReadValueF64(endian);
                            sb.Append(value.ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                        writer.WriteValue(sb.ToString());
                        break;
                    }

                default:
                {
                    var elementTypeDefinition = runtime.GetTypeDefinition(typeDefinition.ElementTypeHash);
                    switch (elementTypeDefinition.Type)
                    {
                        case TypeDefinitionType.Structure:
                        {
                            for (long i = 0; i < count; i++)
                            {
                                data.Position = offset + (i * elementTypeDefinition.Size);
                                WriteStructure(
                                    writer,
                                    elementTypeDefinition,
                                    -1,
                                    null,
                                    data,
                                    endian,
                                    runtime,
                                    ref counter,
                                    queue);
                            }
                            break;
                        }

                        default:
                        {
                            throw new NotSupportedException();
                        }
                    }
                    break;
                }
            }

            writer.WriteEndElement();
        }
    }
}
