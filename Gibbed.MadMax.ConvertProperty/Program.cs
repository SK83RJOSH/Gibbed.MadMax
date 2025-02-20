﻿/* Copyright (c) 2015 Rick (rick 'at' gibbed 'dot' us)
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
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Gibbed.IO;
using Gibbed.MadMax.FileFormats;
using Gibbed.MadMax.PropertyFormats;
using NDesk.Options;
using static Gibbed.MadMax.ConvertProperty.Program;

namespace Gibbed.MadMax.ConvertProperty
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

        private static ProjectData.HashList<uint> _Names;

        internal enum Mode
        {
            Unknown,
            Export,
            Import,
        }

        internal enum FileFormat
        {
            // ReSharper disable InconsistentNaming
            Bin, // RawPropertyContainerFile
            RTPC, // PropertyContainerFile
            // ReSharper restore InconsistentNaming
        }

        public static void Main(string[] args)
        {
            var mode = Mode.Unknown;
            Endian? endian = null;
            bool showHelp = false;
            string currentProject = null;

            var options = new OptionSet
            {
                // ReSharper disable AccessToModifiedClosure
                { "e|export", "convert from binary to XML", v => SetOption(v, ref mode, Mode.Export) },
                { "i|import", "convert from XML to binary", v => SetOption(v, ref mode, Mode.Import) },
                { "l|little-endian", "write in little endian mode", v => SetOption(v, ref endian, Endian.Little) },
                { "b|big-endian", "write in big endian mode", v => SetOption(v, ref endian, Endian.Big) },
                // ReSharper restore AccessToModifiedClosure
                { "p|project=", "override current project", v => currentProject = v },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;
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

            //extras.Add("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mad Max\\dropzone\\locations\\a00\\character\\madmax\\upgrade\\max_upgrade_shotgun_lvl5.xml");
            //extras.Add("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mad Max\\archives_win64_unpacked\\locations\\world.bin");

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
                Console.WriteLine("Usage: {0} [OPTIONS]+ [-e] input_bin [output_xml]", GetExecutableName());
                Console.WriteLine("       {0} [OPTIONS]+ [-i] input_xml [output_bin]", GetExecutableName());
                Console.WriteLine("Convert a property file between binary and XML format.");
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var manager = ProjectData.Manager.Load(currentProject);
            if (manager.ActiveProject == null)
            {
                Console.WriteLine("Warning: no active project loaded.");
            }

            _Names = manager.LoadPropertyNames();

            if (mode == Mode.Export)
            {
                if (endian == null)
                {
                    endian = manager.GetSetting("endian", Endian.Little);
                }

                string inputPath = extras[0];
                string outputPath = extras.Count > 1
                                        ? extras[1]
                                        : Path.ChangeExtension(inputPath, ".xml");

                var extension = Path.GetExtension(inputPath);

                IPropertyFile propertyFile;
                FileFormat fileFormat;

                using (var input = File.OpenRead(inputPath))
                {
                    input.Seek(0, SeekOrigin.Begin);

                    if (PropertyContainerFile.CheckSignature(input) == true)
                    {
                        fileFormat = FileFormat.RTPC;

                        input.Seek(0, SeekOrigin.Begin);
                        var propertyContainerFile = new PropertyContainerFile();
                        propertyContainerFile.Deserialize(input);
                        endian = propertyContainerFile.Endian;
                        propertyFile = propertyContainerFile;
                    }
                    else
                    {
                        fileFormat = FileFormat.Bin;

                        input.Seek(0, SeekOrigin.Begin);
                        propertyFile = new RawPropertyContainerFile()
                        {
                            Endian = endian.Value,
                        };
                        propertyFile.Deserialize(input);
                    }
                }

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "\t",
                    CheckCharacters = false,
                };

                using (var output = File.Create(outputPath))
                using (var writer = XmlWriter.Create(output, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("container");

                    if (extension != null)
                    {
                        writer.WriteAttributeString("extension", extension);
                    }

                    writer.WriteAttributeString("format", fileFormat.ToString());
                    writer.WriteAttributeString("endian", endian.Value.ToString());

                    foreach (var node in propertyFile.Nodes)
                    {
                        writer.WriteStartElement("object");
                        WriteObject(writer, node);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
            else if (mode == Mode.Import)
            {
                string inputPath = extras[0];

                IPropertyFile propertyFile;
                string extension;

                using (var input = File.OpenRead(inputPath))
                {
                    var doc = new XPathDocument(input);
                    var nav = doc.CreateNavigator();

                    var root = nav.SelectSingleNode("/container");
                    if (root == null)
                    {
                        throw new FormatException();
                    }

                    extension = root.GetAttribute("extension", "");
                    if (string.IsNullOrEmpty(extension) == true)
                    {
                        extension = ".bin";
                    }

                    FileFormat fileFormat;
                    var formatAttribute = root.GetAttribute("format", "");
                    if (string.IsNullOrEmpty(formatAttribute) == false)
                    {
                        if (Enum.TryParse(formatAttribute, true, out fileFormat) == false)
                        {
                            throw new FormatException();
                        }
                    }
                    else
                    {
                        fileFormat = FileFormat.Bin;
                    }

                    var endianAttribute = root.GetAttribute("endian", "");
                    if (endian.HasValue == false &&
                        string.IsNullOrEmpty(endianAttribute) == false)
                    {
                        Endian fileEndian;
                        if (Enum.TryParse(endianAttribute, out fileEndian) == false)
                        {
                            throw new FormatException();
                        }

                        endian = fileEndian;
                    }
                    else
                    {
                        endian = manager.GetSetting("endian", Endian.Little);
                    }

                    switch (fileFormat)
                    {
                        case FileFormat.Bin:
                        {
                            propertyFile = new RawPropertyContainerFile()
                            {
                                Endian = endian.Value,
                            };

                                var nodes = root.Select("object");
                                while (nodes.MoveNext() == true)
                                //    var node = root.SelectSingleNode("object");
                                //if (node != null)
                                {
                                    propertyFile.Nodes.Add(ParseObject(nodes.Current, true));
                                    //node = node.SelectSingleNode("object");
                                    //propertyFile.Root = ParseObject(node);
                                }

                                break;
                        }

                        case FileFormat.RTPC:
                        {
                            propertyFile = new PropertyContainerFile()
                            {
                                Endian = endian.Value,
                            };

                            var node = root.SelectSingleNode("object");
                            if (node != null)
                            {
                                node = node.SelectSingleNode("object");
                                propertyFile.Root = ParseObject(node, false);
                            }
                            break;
                        }

                        default:
                        {
                            throw new NotSupportedException();
                        }
                    }
                   
                }

                string outputPath = extras.Count > 1
                                        ? extras[1]
                                        : Path.ChangeExtension(inputPath, extension);

                using (var output = File.Create(outputPath))
                {
                    propertyFile.Serialize(output);
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static void WriteProperty(XmlWriter writer, IVariant variant)
        {
            writer.WriteAttributeString("type", variant.Tag);
            writer.WriteValue(variant.Compose(_Names));
        }

        private static void WriteObject(XmlWriter writer, Node node)
        {
            if (node.Tag != null)
            {
                writer.WriteAttributeString("tag", node.Tag);
            }

            // is this ridiculous?
            // yeeeeep.

            var childrenByName =
                node.Children
                    .Where(kv => node.KnownNames.ContainsKey(kv.Key) == true)
                    .Select(kv => new KeyValuePair<string, Node>(node.KnownNames[kv.Key], kv.Value))
                    .Concat(node.Children
                                .Where(kv => node.KnownNames.ContainsKey(kv.Key) == false &&
                                             _Names.Contains(kv.Key) == true)
                                .Select(kv => new KeyValuePair<string, Node>(_Names[kv.Key], kv.Value)))
                    .ToArray();

            var childrenByNameHash =
                node.Children
                    .Where(kv => node.KnownNames.ContainsKey(kv.Key) == false &&
                                 _Names.Contains(kv.Key) == false)
                    .Select(kv => kv)
                    .ToArray();

            var propertiesByName =
                node.Properties
                    .Where(kv => node.KnownNames.ContainsKey(kv.Key) == true)
                    .Select(kv => new KeyValuePair<string, IVariant>(node.KnownNames[kv.Key], kv.Value))
                    .Concat(node.Properties
                                .Where(kv => node.KnownNames.ContainsKey(kv.Key) == false &&
                                             _Names.Contains(kv.Key) == true)
                                .Select(kv => new KeyValuePair<string, IVariant>(
                                                  _Names[kv.Key],
                                                  kv.Value)))
                    .ToArray();

            var propertiesByNameHash =
                node.Properties
                    .Where(kv => node.KnownNames.ContainsKey(kv.Key) == false &&
                                 _Names.Contains(kv.Key) == false)
                    .Select(kv => kv)
                    .ToArray();

            if (propertiesByName.Length > 0)
            {
                foreach (var kv in propertiesByName.OrderBy(kv => kv.Key))
                {
                    writer.WriteStartElement("value");
                    writer.WriteAttributeString("name", kv.Key);
                    WriteProperty(writer, kv.Value);
                    writer.WriteEndElement();
                }
            }

            if (childrenByName.Length > 0)
            {
                foreach (var kv in childrenByName.OrderBy(kv => kv.Key))
                {
                    writer.WriteStartElement("object");
                    writer.WriteAttributeString("name", kv.Key);
                    WriteObject(writer, kv.Value);
                    writer.WriteEndElement();
                }
            }

            if (propertiesByNameHash.Length > 0)
            {
                foreach (var kv in propertiesByNameHash.OrderBy(p => p.Key, new NameComparer(_Names)))
                {
                    writer.WriteStartElement("value");

                    if (_Names.Contains(kv.Key) == true)
                    {
                        writer.WriteAttributeString("name", _Names[kv.Key]);
                    }
                    else
                    {
                        writer.WriteAttributeString("id", kv.Key.ToString("X8"));
                    }

                    WriteProperty(writer, kv.Value);
                    writer.WriteEndElement();
                }
            }

            if (childrenByNameHash.Length > 0)
            {
                foreach (var kv in childrenByNameHash.OrderBy(n => n.Key, new NameComparer(_Names)))
                {
                    writer.WriteStartElement("object");

                    if (_Names.Contains(kv.Key) == true)
                    {
                        writer.WriteAttributeString("name", _Names[kv.Key]);
                    }
                    else
                    {
                        writer.WriteAttributeString("id", kv.Key.ToString("X8"));
                    }

                    WriteObject(writer, kv.Value);
                    writer.WriteEndElement();
                }
            }
        }

        private static uint GetIdOrName(XPathNavigator node, out string name)
        {
            string id = node.GetAttribute("id", "");
            name = node.GetAttribute("name", "");

            if (string.IsNullOrEmpty(id) == false &&
                string.IsNullOrEmpty(name) == false)
            {
                if (uint.Parse(id, NumberStyles.AllowHexSpecifier) !=
                    name.HashJenkins())
                {
                    throw new InvalidOperationException("supplied id and name, but they don't match");
                }
            }
            else if (string.IsNullOrEmpty(id) == true &&
                     string.IsNullOrEmpty(name) == true)
            {
                throw new InvalidOperationException("did not supply id or name");
            }

            if (string.IsNullOrEmpty(id) == false)
            {
                return uint.Parse(id, NumberStyles.AllowHexSpecifier);
            }

            return name.HashJenkins();
        }

        private static Node ParseObject(XPathNavigator nav, bool skipHash)
        {
            var node = new Node();
            string name = "";
            if (!skipHash)
                node.NameHash = GetIdOrName(nav, out name);

            if (nav.MoveToAttribute("tag", "") == true)
            {
                node.Tag = nav.Value;
                nav.MoveToParent();
            }

            var values = nav.Select("value");
            while (values.MoveNext() == true)
            {
                var current = values.Current;
                if (current == null)
                {
                    throw new InvalidOperationException();
                }

                var id = GetIdOrName(current, out name);
                //node.NameHash = id;
                var type = current.GetAttribute("type", "");

                var variant = VariantFactory.GetVariant(type);
                variant.Parse(current.Value);

                if (node.Properties.ContainsKey(id) == true)
                {
                    var lineInfo = (IXmlLineInfo)current;

                    if (string.IsNullOrEmpty(name) == true)
                    {
                        throw new FormatException(string.Format(
                            "duplicate object id 0x{0:X8} at line {1} position {2}",
                            id,
                            lineInfo.LineNumber,
                            lineInfo.LinePosition));
                    }

                    throw new FormatException(
                        string.Format("duplicate object id 0x{0:X8} ('{1}') at line {2} position {3}",
                                      id,
                                      name,
                                      lineInfo.LineNumber,
                                      lineInfo.LinePosition));
                }

                node.Properties.Add(id, variant);
            }

            var children = nav.Select("object");
            while (children.MoveNext() == true)
            {
                var child = children.Current;
                if (child == null)
                {
                    throw new InvalidOperationException();
                }
                var obj = ParseObject(child, false);

                if (node.Children.ContainsKey(obj.NameHash) == true)
                {
                    var lineInfo = (IXmlLineInfo)child;

                    if (string.IsNullOrEmpty(name) == true)
                    {
                        throw new FormatException(string.Format(
                            "duplicate object id 0x{0:X8} at line {1} position {2}",
                            obj.NameHash,
                            lineInfo.LineNumber,
                            lineInfo.LinePosition));
                    }

                    throw new FormatException(
                        string.Format("duplicate object id 0x{0:X8} ('{1}') at line {2} position {3}",
                                      obj.NameHash,
                                      name,
                                      lineInfo.LineNumber,
                                      lineInfo.LinePosition));
                }

                node.Children.Add(obj.NameHash, obj);
            }

            return node;
        }
    }
}
