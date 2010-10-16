﻿// --------------------------------
// <copyright file="JsonReader.cs" company="Nikhil Kothari">
//     Copyright (c) 2010 Nikhil Kothari
// </copyright>
// <author>Nikhil Kothari (http://www.nikhilk.net)</author>
// <license>Included in this library with permission. Released under the terms of the Microsoft Public License (Ms-PL)</license>
// <website>http://github.com/NikhilK/dynamicrest</website>
// ---------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;

namespace Facebook
{

    internal sealed class JsonReader : IDisposable
    {
        private TextReader _reader;

        public JsonReader(string jsonText)
        {
            Contract.Requires(!String.IsNullOrEmpty(jsonText));

            _reader = new StringReader(jsonText);
        }

        public JsonReader(TextReader reader)
        {
            Contract.Requires(reader != null);

            _reader = reader;
        }

        [ContractInvariantMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        private void InvarientObject()
        {
            Contract.Invariant(_reader != null);
        }

        private char GetNextCharacter()
        {
            return (char)_reader.Read();
        }

        private string GetCharacters(int count)
        {
            string s = String.Empty;
            for (int i = 0; i < count; i++)
            {
                char ch = (char)_reader.Read();
                if (ch == '\0')
                {
                    return null;
                }
                s += ch;
            }
            return s;
        }

        private char PeekNextSignificantCharacter()
        {
            char ch = (char)_reader.Peek();
            while ((ch != '\0') && Char.IsWhiteSpace(ch))
            {
                _reader.Read();
                ch = (char)_reader.Peek();
            }
            return ch;
        }

        private JsonArray ReadArray()
        {
            JsonArray array = new JsonArray();
            ICollection<object> arrayItems = (ICollection<object>)array;

            // Consume the '['
            _reader.Read();

            while (true)
            {
                char ch = PeekNextSignificantCharacter();
                if (ch == '\0')
                {
                    throw new FormatException("Unterminated array literal.");
                }

                if (ch == ']')
                {
                    _reader.Read();
                    return array;
                }

                if (arrayItems.Count != 0)
                {
                    if (ch != ',')
                    {
                        throw new FormatException("Invalid array literal.");
                    }
                    else
                    {
                        _reader.Read();
                    }
                }

                object item = ReadValue();
                arrayItems.Add(item);
            }
        }

        private bool ReadBoolean()
        {
            string s = ReadName(/* allowQuotes */ false);

            if (s != null)
            {
                if (s.Equals("true", StringComparison.Ordinal))
                {
                    return true;
                }
                else if (s.Equals("false", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            throw new FormatException("Invalid boolean literal.");
        }

        private string ReadName(bool allowQuotes)
        {
            char ch = PeekNextSignificantCharacter();

            if ((ch == '"') || (ch == '\''))
            {
                if (allowQuotes)
                {
                    return ReadString();
                }
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                while (true)
                {
                    ch = (char)_reader.Peek();
                    if ((ch == '_') || Char.IsLetterOrDigit(ch))
                    {
                        _reader.Read();
                        sb.Append(ch);
                    }
                    else
                    {
                        return sb.ToString();
                    }
                }
            }

            return null;
        }

        private void ReadNull()
        {
            string s = ReadName(/* allowQuotes */ false);

            if ((s == null) || !s.Equals("null", StringComparison.Ordinal))
            {
                throw new FormatException("Invalid null literal.");
            }
        }

        private object ReadNumber()
        {
            char ch = (char)_reader.Read();

            StringBuilder sb = new StringBuilder();
            bool hasDecimal = (ch == '.');

            sb.Append(ch);
            while (true)
            {
                ch = PeekNextSignificantCharacter();

                if (Char.IsDigit(ch) || (ch == '.'))
                {
                    hasDecimal = hasDecimal || (ch == '.');

                    _reader.Read();
                    sb.Append(ch);
                }
                else
                {
                    break;
                }
            }

            string s = sb.ToString();
            if (hasDecimal)
            {
                float value;
                if (Single.TryParse(s, out value))
                {
                    return value;
                }
            }
            else
            {
                int value;
                if (Int32.TryParse(s, out value))
                {
                    return value;
                }
                else
                {
                    long lvalue;
                    if (Int64.TryParse(s, out lvalue))
                    {
                        return lvalue;
                    }
                }
            }

            throw new FormatException("Invalid numeric literal.");
        }

        private JsonObject ReadObject()
        {
            JsonObject record = new JsonObject();
            IDictionary<string, object> recordItems = (IDictionary<string, object>)record;

            // Consume the '{'
            _reader.Read();

            while (true)
            {
                char ch = PeekNextSignificantCharacter();
                if (ch == '\0')
                {
                    throw new FormatException("Unterminated object literal.");
                }

                if (ch == '}')
                {
                    _reader.Read();
                    return record;
                }

                if (recordItems.Count != 0)
                {
                    if (ch != ',')
                    {
                        throw new FormatException("Invalid object literal.");
                    }
                    else
                    {
                        _reader.Read();
                    }
                }

                string name = ReadName(/* allowQuotes */ true);
                ch = PeekNextSignificantCharacter();

                if (ch != ':')
                {
                    throw new FormatException("Unexpected name/value pair syntax in object literal.");
                }
                else
                {
                    _reader.Read();
                }

                object item = ReadValue(name);
                recordItems[name] = item;
            }
        }

        private string ReadString()
        {
            bool dummy;
            return ReadString(out dummy);
        }

        private string ReadString(out bool hasLeadingSlash)
        {
            StringBuilder sb = new StringBuilder();

            char endQuoteCharacter = (char)_reader.Read();
            bool inEscape = false;
            bool firstCharacter = true;

            hasLeadingSlash = false;

            while (true)
            {
                char ch = GetNextCharacter();
                if (ch == '\0')
                {
                    throw new FormatException("Unterminated string literal.");
                }
                if (firstCharacter)
                {
                    if (ch == '\\')
                    {
                        hasLeadingSlash = true;
                    }
                    firstCharacter = false;
                }

                if (inEscape)
                {
                    if (ch == 'u')
                    {
                        string unicodeSequence = GetCharacters(4);
                        if (unicodeSequence == null)
                        {
                            throw new FormatException("Unterminated string literal.");
                        }
                        ch = (char)Int32.Parse(unicodeSequence, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    }

                    sb.Append(ch);
                    inEscape = false;
                    continue;
                }

                if (ch == '\\')
                {
                    inEscape = true;
                    continue;
                }

                if (ch == endQuoteCharacter)
                {
                    return sb.ToString();
                }

                sb.Append(ch);
            }
        }

        public object ReadValue()
        {
            return ReadValue(null);
        }

        public object ReadValue(string name)
        {
            object value = null;
            bool allowNull = false;

            char ch = PeekNextSignificantCharacter();
            if (ch == '[')
            {
                value = ReadArray();
            }
            else if (ch == '{')
            {
                value = ReadObject();
            }
            else if ((ch == '\'') || (ch == '"'))
            {
                bool hasLeadingSlash;
                string s = ReadString(out hasLeadingSlash);

                if (!String.IsNullOrEmpty(name))
                {
                    // Convert date strings to DateTime values
                    if (name.EndsWith("_time", StringComparison.Ordinal) || name.EndsWith("_date", StringComparison.Ordinal))
                    {
                        DateTime dtValue;
                        if (DateTime.TryParse(s, out dtValue))
                        {
                            value = dtValue;
                        }
                    }
                }

                if (value == null)
                {
                    value = s;
                }
            }
            else if (Char.IsDigit(ch) || (ch == '-') || (ch == '.'))
            {
                value = ReadNumber();
            }
            else if ((ch == 't') || (ch == 'f'))
            {
                value = ReadBoolean();
            }
            else if (ch == 'n')
            {
                ReadNull();
                allowNull = true;
            }

            if ((value == null) && (allowNull == false))
            {
                throw new FormatException("Invalid JSON text.");
            }
            return value;
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }
        }
    }
}