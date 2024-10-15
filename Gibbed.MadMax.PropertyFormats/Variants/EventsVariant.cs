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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gibbed.IO;
using Gibbed.MadMax.FileFormats;

namespace Gibbed.MadMax.PropertyFormats.Variants
{
    //SEventID
    // unsigned int m_Hash;
    // unsigned int m_NameSpace;
    
    public class EventsVariant : IVariant, RawPropertyContainerFile.IRawVariant, PropertyContainerFile.IRawVariant
    {
        private readonly List<KeyValuePair<uint, uint>> _Values;

        public EventsVariant()
        {
            this._Values = new List<KeyValuePair<uint, uint>>();
        }

        public List<KeyValuePair<uint, uint>> Values
        {
            get { return this._Values; }
        }

        public string Tag
        {
            get { return "vec_events"; }
        }

        private uint ParseElement(string element)
        {
            element = element.Trim();

            if (element.StartsWith("0x") || element.StartsWith("0X"))
            {
                return uint.Parse(element.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
               return StringHelpers.HashJenkins(element);
            }
        }

        public void Parse(string text)
        {
            this._Values.Clear();
            if (string.IsNullOrEmpty(text) == false)
            {
                var parts = text.Split(',');
                if ((parts.Length % 2) != 0)
                {
                    throw new FormatException("vec_events requires pairs of uints delimited by a comma");
                }
                for (int i = 0; i < parts.Length; i += 2)
                {
                    var left = ParseElement(parts[i + 0]);
                    var right = ParseElement(parts[i + 1]);
                    this._Values.Add(new KeyValuePair<uint, uint>(left, right));
                }
            }
        }

        public string Compose(ProjectData.HashList<uint> hashNames)
        {
            return string.Join(", ", this._Values.Select(v => Compose(v, hashNames)));
        }

        private static string Compose(KeyValuePair<uint, uint> kv, ProjectData.HashList<uint> hashNames)
        {
            if(hashNames.Contains(kv.Key))
            {
                return string.Format(
                "{0},0x{1:X}",
                hashNames[kv.Key],
                kv.Value);
            }

            return string.Format(
                "0x{0:X},0x{1:X}",
                kv.Key,
                kv.Value);
        }

        #region RawPropertyContainerFile
        RawPropertyContainerFile.VariantType RawPropertyContainerFile.IRawVariant.Type
        {
            get { return RawPropertyContainerFile.VariantType.Events; }
        }

        void RawPropertyContainerFile.IRawVariant.Serialize(Stream output, Endian endian)
        {
            var values = this._Values;
            output.WriteValueS32(values.Count, endian);
            foreach (var kv in values)
            {
                output.WriteValueU32(kv.Key, endian);
                output.WriteValueU32(kv.Value, endian);
            }
        }

        void RawPropertyContainerFile.IRawVariant.Deserialize(Stream input, Endian endian)
        {
            int count = input.ReadValueS32(endian);
            var values = new KeyValuePair<uint, uint>[count];
            for (int i = 0; i < count; i++)
            {
                var left = input.ReadValueU32(endian);
                var right = input.ReadValueU32(endian);
                values[i] = new KeyValuePair<uint, uint>(left, right);
            }
            this._Values.Clear();
            this._Values.AddRange(values);
        }
        #endregion

        #region PropertyContainerFile
        PropertyContainerFile.VariantType PropertyContainerFile.IRawVariant.Type
        {
            get { return PropertyContainerFile.VariantType.Events; }
        }

        bool PropertyContainerFile.IRawVariant.IsSimple
        {
            get { return true; }
        }

        uint PropertyContainerFile.IRawVariant.Alignment
        {
            get { return 4; }
        }

        void PropertyContainerFile.IRawVariant.Serialize(Stream output, Endian endian)
        {
            var values = this._Values;
            output.WriteValueS32(values.Count, endian);
            foreach (var value in values)
            {
                output.WriteValueU32(value.Key, endian);
                output.WriteValueU32(value.Value, endian);
            }
        }

        void PropertyContainerFile.IRawVariant.Deserialize(Stream input, Endian endian)
        {
            int count = input.ReadValueS32(endian);
            var values = new KeyValuePair<uint, uint>[count];
            for (int i = 0; i < count; i++)
            {
                var left = input.ReadValueU32(endian);
                var right = input.ReadValueU32(endian);
                values[i] = new KeyValuePair<uint, uint>(left, right);
            }
            this._Values.Clear();
            this._Values.AddRange(values);
        }
        #endregion
    }
}
