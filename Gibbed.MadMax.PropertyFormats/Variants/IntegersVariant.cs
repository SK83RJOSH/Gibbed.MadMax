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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gibbed.IO;

namespace Gibbed.MadMax.PropertyFormats.Variants
{
    public class IntegersVariant : IVariant, RawPropertyContainerFile.IRawVariant, PropertyContainerFile.IRawVariant
    {
        private readonly List<int> _Values;

        public IntegersVariant()
        {
            this._Values = new List<int>();
        }

        public string Tag
        {
            get { return "vec_int"; }
        }

        public void Parse(string text)
        {
            this._Values.Clear();
            if (string.IsNullOrEmpty(text) == false)
            {
                var parts = text.Split(',');
                foreach (var part in parts)
                {
                    this._Values.Add(int.Parse(part, CultureInfo.InvariantCulture));
                }
            }
        }

        public string Compose(ProjectData.HashList<uint> hashNames)
        {
            return string.Join(",", this._Values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
        }

        #region RawPropertyContainerFile
        RawPropertyContainerFile.VariantType RawPropertyContainerFile.IRawVariant.Type
        {
            get { return RawPropertyContainerFile.VariantType.Integers; }
        }

        void RawPropertyContainerFile.IRawVariant.Serialize(Stream output, Endian endian)
        {
            var values = this._Values;
            output.WriteValueS32(values.Count, endian);
            foreach (int value in values)
            {
                output.WriteValueS32(value, endian);
            }
        }

        void RawPropertyContainerFile.IRawVariant.Deserialize(Stream input, Endian endian)
        {
            int count = input.ReadValueS32(endian);
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = input.ReadValueS32(endian);
            }
            this._Values.Clear();
            this._Values.AddRange(values);
        }
        #endregion

        #region PropertyContainerFile
        PropertyContainerFile.VariantType PropertyContainerFile.IRawVariant.Type
        {
            get { return PropertyContainerFile.VariantType.Integers; }
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
            foreach (int value in values)
            {
                output.WriteValueS32(value, endian);
            }
        }

        void PropertyContainerFile.IRawVariant.Deserialize(Stream input, Endian endian)
        {
            int count = input.ReadValueS32(endian);
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = input.ReadValueS32(endian);
            }
            this._Values.Clear();
            this._Values.AddRange(values);
        }
        #endregion
    }
}
