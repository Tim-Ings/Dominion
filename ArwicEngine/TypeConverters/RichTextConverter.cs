﻿// Dominion - Copyright (C) Timothy Ings
// RichTextConverter.cs
// This file contains classes that define a type converter for rich text

using ArwicEngine.Forms;
using Microsoft.Xna.Framework;
using System;
using System.ComponentModel;
using System.Globalization;

namespace ArwicEngine.TypeConverters
{
    public class RichTextConverter : TypeConverter
    {
        // Overrides the CanConvertFrom method of TypeConverter.
        // The ITypeDescriptorContext interface provides the context for the
        // conversion. Typically, this interface is used at design time to 
        // provide information about the design-time container.
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;
            return base.CanConvertFrom(context, sourceType);
        }
        // Overrides the ConvertFrom method of TypeConverter.
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                return RichText.ParseText((string)value);
            }
            return base.ConvertFrom(context, culture, value);
        }
        // Overrides the ConvertTo method of TypeConverter.
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                return ((RichText)value).Text;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
