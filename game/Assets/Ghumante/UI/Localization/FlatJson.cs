using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ghumante.UI.Localization
{
    /// <summary>
    /// Minimal parser for a flat JSON object of string values: <c>{"key": "value", ...}</c>.
    /// String tables use this shape so translators can edit them in any text editor. JsonUtility cannot
    /// read dictionaries, and we do not want a JSON package dependency in the UI assembly for M0.
    /// Nested objects, arrays, numbers, booleans and null are rejected with a <see cref="FormatException"/>.
    /// </summary>
    public static class FlatJson
    {
        public static Dictionary<string, string> Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var reader = new Reader(json);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            reader.SkipBom();
            reader.SkipWhitespace();
            reader.Expect('{');
            reader.SkipWhitespace();
            if (reader.TryConsume('}'))
            {
                reader.ExpectEnd();
                return result;
            }

            while (true)
            {
                reader.SkipWhitespace();
                string key = reader.ReadString();
                reader.SkipWhitespace();
                reader.Expect(':');
                reader.SkipWhitespace();
                string value = reader.ReadString();
                if (result.ContainsKey(key))
                {
                    throw reader.Error("duplicate key \"" + key + "\"");
                }
                result.Add(key, value);
                reader.SkipWhitespace();
                if (reader.TryConsume(',')) continue;
                reader.Expect('}');
                reader.ExpectEnd();
                return result;
            }
        }

        private sealed class Reader
        {
            private readonly string _s;
            private int _i;

            public Reader(string s)
            {
                _s = s;
            }

            public void SkipBom()
            {
                if (_i < _s.Length && _s[_i] == '﻿') _i++;
            }

            public void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            public bool TryConsume(char c)
            {
                if (_i < _s.Length && _s[_i] == c)
                {
                    _i++;
                    return true;
                }
                return false;
            }

            public void Expect(char c)
            {
                if (!TryConsume(c)) throw Error("expected '" + c + "'");
            }

            public void ExpectEnd()
            {
                SkipWhitespace();
                if (_i != _s.Length) throw Error("unexpected trailing content");
            }

            public string ReadString()
            {
                if (_i >= _s.Length || _s[_i] != '"') throw Error("expected a string (only string values are allowed)");
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("control character in string");
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_i >= _s.Length) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("truncated \\u escape");
                            int code;
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                throw Error("invalid \\u escape");
                            }
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default:
                            throw Error("invalid escape \\" + e);
                    }
                }
            }

            public FormatException Error(string message)
            {
                int line = 1, col = 1;
                for (int k = 0; k < _i && k < _s.Length; k++)
                {
                    if (_s[k] == '\n')
                    {
                        line++;
                        col = 1;
                    }
                    else
                    {
                        col++;
                    }
                }
                return new FormatException("String table JSON: " + message + " at line " + line + ", column " + col + ".");
            }
        }
    }
}
