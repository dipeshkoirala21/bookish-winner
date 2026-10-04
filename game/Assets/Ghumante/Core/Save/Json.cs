using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ghumante.Core.Save
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    /// <summary>Malformed JSON text, with the character offset of the problem.</summary>
    public sealed class JsonParseException : FormatException
    {
        public readonly int Offset;

        public JsonParseException(string message, int offset) : base(message + " at offset " + offset)
        {
            Offset = offset;
        }
    }

    /// <summary>
    /// A small dependency-free JSON value (Core may not use Newtonsoft or System.Text.Json). Integers are kept
    /// exactly as <see cref="long"/> when the literal has no fraction or exponent and fits; other numbers are
    /// doubles. Objects keep insertion order and reject duplicate keys when parsed.
    /// </summary>
    public class JsonValue
    {
        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);
        public static readonly JsonValue True = new JsonValue(JsonKind.Bool) { _bool = true };
        public static readonly JsonValue False = new JsonValue(JsonKind.Bool);

        public readonly JsonKind Kind;
        private bool _bool;
        private bool _isInt;
        private long _int;
        private double _num;
        private string _str;

        protected JsonValue(JsonKind kind)
        {
            Kind = kind;
        }

        public static JsonValue From(bool b)
        {
            return b ? True : False;
        }

        public static JsonValue From(long v)
        {
            return new JsonValue(JsonKind.Number) { _isInt = true, _int = v, _num = v };
        }

        public static JsonValue From(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("JSON numbers must be finite");
            return new JsonValue(JsonKind.Number) { _num = v };
        }

        public static JsonValue From(string s)
        {
            return s == null ? Null : new JsonValue(JsonKind.String) { _str = s };
        }

        public bool IsNull
        {
            get { return Kind == JsonKind.Null; }
        }

        /// <summary>True for numbers held exactly as a long.</summary>
        public bool IsInteger
        {
            get { return Kind == JsonKind.Number && _isInt; }
        }

        public bool AsBool(bool fallback = false)
        {
            return Kind == JsonKind.Bool ? _bool : fallback;
        }

        public double AsDouble(double fallback = 0.0)
        {
            return Kind == JsonKind.Number ? _num : fallback;
        }

        /// <summary>The number as a long (integral doubles in range convert too), else the fallback.</summary>
        public long AsLong(long fallback = 0)
        {
            if (Kind != JsonKind.Number) return fallback;
            if (_isInt) return _int;
            if (Math.Floor(_num) == _num && _num >= -9.2233720368547758E18 && _num < 9.2233720368547758E18) return (long)_num;
            return fallback;
        }

        public int AsInt(int fallback = 0)
        {
            long v = AsLong(fallback);
            return v < int.MinValue || v > int.MaxValue ? fallback : (int)v;
        }

        public string AsString(string fallback = null)
        {
            return Kind == JsonKind.String ? _str : fallback;
        }

        public JsonObject AsObject()
        {
            return this as JsonObject;
        }

        public JsonArray AsArray()
        {
            return this as JsonArray;
        }

        public override string ToString()
        {
            return Json.Write(this);
        }

        internal void WriteScalar(StringBuilder sb)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(_bool ? "true" : "false");
                    break;
                case JsonKind.Number:
                    if (_isInt) sb.Append(_int.ToString(CultureInfo.InvariantCulture));
                    else Json.WriteDouble(sb, _num);
                    break;
                case JsonKind.String:
                    Json.WriteString(sb, _str);
                    break;
            }
        }

        internal bool ScalarEquals(JsonValue o)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    return true;
                case JsonKind.Bool:
                    return _bool == o._bool;
                case JsonKind.Number:
                    return _isInt && o._isInt ? _int == o._int : _num.Equals(o._num);
                default:
                    return string.Equals(_str, o._str, StringComparison.Ordinal);
            }
        }

        /// <summary>Structural equality (object key order ignored).</summary>
        public static bool DeepEquals(JsonValue a, JsonValue b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Kind != b.Kind) return false;
            if (a.Kind == JsonKind.Array)
            {
                JsonArray x = (JsonArray)a, y = (JsonArray)b;
                if (x.Count != y.Count) return false;
                for (int i = 0; i < x.Count; i++)
                    if (!DeepEquals(x[i], y[i])) return false;
                return true;
            }
            if (a.Kind == JsonKind.Object)
            {
                JsonObject x = (JsonObject)a, y = (JsonObject)b;
                if (x.Count != y.Count) return false;
                foreach (var kv in x)
                {
                    JsonValue v;
                    if (!y.TryGetValue(kv.Key, out v) || !DeepEquals(kv.Value, v)) return false;
                }
                return true;
            }
            return a.ScalarEquals(b);
        }
    }

    public sealed class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        private readonly List<JsonValue> _items = new List<JsonValue>();

        public JsonArray() : base(JsonKind.Array)
        {
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public JsonValue this[int i]
        {
            get { return _items[i]; }
            set { _items[i] = value ?? Null; }
        }

        public JsonArray Add(JsonValue v)
        {
            _items.Add(v ?? Null);
            return this;
        }

        public JsonArray Add(string s)
        {
            return Add(From(s));
        }

        public JsonArray Add(long v)
        {
            return Add(From(v));
        }

        public JsonArray Add(double v)
        {
            return Add(From(v));
        }

        public JsonArray Add(bool v)
        {
            return Add(From(v));
        }

        public IEnumerator<JsonValue> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }

    public sealed class JsonObject : JsonValue, IEnumerable<KeyValuePair<string, JsonValue>>
    {
        private readonly List<KeyValuePair<string, JsonValue>> _items = new List<KeyValuePair<string, JsonValue>>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        public JsonObject() : base(JsonKind.Object)
        {
        }

        public int Count
        {
            get { return _items.Count; }
        }

        /// <summary>Get (null when absent) or set (replacing in place, else appending) a member.</summary>
        public JsonValue this[string key]
        {
            get
            {
                int i;
                return _index.TryGetValue(key, out i) ? _items[i].Value : null;
            }
            set { Set(key, value); }
        }

        public bool ContainsKey(string key)
        {
            return _index.ContainsKey(key);
        }

        public bool TryGetValue(string key, out JsonValue value)
        {
            int i;
            if (_index.TryGetValue(key, out i))
            {
                value = _items[i].Value;
                return true;
            }
            value = null;
            return false;
        }

        public JsonObject Set(string key, JsonValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            value = value ?? Null;
            int i;
            if (_index.TryGetValue(key, out i)) _items[i] = new KeyValuePair<string, JsonValue>(key, value);
            else
            {
                _index[key] = _items.Count;
                _items.Add(new KeyValuePair<string, JsonValue>(key, value));
            }
            return this;
        }

        public JsonObject Set(string key, string value)
        {
            return Set(key, From(value));
        }

        public JsonObject Set(string key, long value)
        {
            return Set(key, From(value));
        }

        public JsonObject Set(string key, double value)
        {
            return Set(key, From(value));
        }

        public JsonObject Set(string key, bool value)
        {
            return Set(key, From(value));
        }

        public bool Remove(string key)
        {
            int i;
            if (!_index.TryGetValue(key, out i)) return false;
            _items.RemoveAt(i);
            _index.Remove(key);
            for (int k = i; k < _items.Count; k++) _index[_items[k].Key] = k;
            return true;
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var kv in _items) yield return kv.Key;
            }
        }

        public JsonObject GetObject(string key)
        {
            return this[key] as JsonObject;
        }

        public JsonArray GetArray(string key)
        {
            return this[key] as JsonArray;
        }

        public string GetString(string key, string fallback = null)
        {
            JsonValue v = this[key];
            return v == null ? fallback : v.AsString(fallback);
        }

        public long GetLong(string key, long fallback = 0)
        {
            JsonValue v = this[key];
            return v == null ? fallback : v.AsLong(fallback);
        }

        public int GetInt(string key, int fallback = 0)
        {
            JsonValue v = this[key];
            return v == null ? fallback : v.AsInt(fallback);
        }

        public double GetDouble(string key, double fallback = 0.0)
        {
            JsonValue v = this[key];
            return v == null ? fallback : v.AsDouble(fallback);
        }

        public bool GetBool(string key, bool fallback = false)
        {
            JsonValue v = this[key];
            return v == null ? fallback : v.AsBool(fallback);
        }

        public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }

    /// <summary>Strict RFC 8259 JSON reader and writer for <see cref="JsonValue"/> trees.</summary>
    public static class Json
    {
        public const int MaxDepth = 128;

        // ---------------------------------------------------------------- writing

        /// <summary>Serialise; <paramref name="indent"/> &gt; 0 pretty-prints with that many spaces.</summary>
        public static string Write(JsonValue value, int indent = 0)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value ?? JsonValue.Null, indent, 0);
            return sb.ToString();
        }

        private static void NewLine(StringBuilder sb, int indent, int level)
        {
            if (indent <= 0) return;
            sb.Append('\n');
            sb.Append(' ', indent * level);
        }

        private static void WriteValue(StringBuilder sb, JsonValue v, int indent, int level)
        {
            if (level > MaxDepth) throw new InvalidOperationException("JSON nesting deeper than " + MaxDepth);
            var obj = v as JsonObject;
            if (obj != null)
            {
                if (obj.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.Append('{');
                bool first = true;
                foreach (var kv in obj)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, indent, level + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(indent > 0 ? ": " : ":");
                    WriteValue(sb, kv.Value, indent, level + 1);
                }
                NewLine(sb, indent, level);
                sb.Append('}');
                return;
            }
            var arr = v as JsonArray;
            if (arr != null)
            {
                if (arr.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.Append('[');
                for (int i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    NewLine(sb, indent, level + 1);
                    WriteValue(sb, arr[i], indent, level + 1);
                }
                NewLine(sb, indent, level);
                sb.Append(']');
                return;
            }
            v.WriteScalar(sb);
        }

        internal static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new InvalidOperationException("JSON numbers must be finite");
            string s = d.ToString("R", CultureInfo.InvariantCulture);
            // Keep a fraction or exponent so the value reads back as a double, not an integer.
            if (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0) s += ".0";
            sb.Append(s);
        }

        /// <summary>Quoted JSON string; non-ASCII text is written as is (UTF-8 files), controls are escaped,
        /// and so are lone surrogates (so the output always encodes to valid UTF-8).</summary>
        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        {
                            sb.Append(c).Append(s[i + 1]);
                            i++;
                        }
                        else if (c < 0x20 || c == '\u2028' || c == '\u2029' || char.IsSurrogate(c))
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------------------------------------------------------- reading

        /// <summary>Parse exactly one JSON value (surrounding whitespace allowed, a leading BOM ignored).</summary>
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            if (p.Pos < text.Length && text[p.Pos] == '﻿') p.Pos++;
            p.SkipWs();
            JsonValue v = p.ParseValue(0);
            p.SkipWs();
            if (p.Pos != text.Length) throw new JsonParseException("trailing characters", p.Pos);
            return v;
        }

        public static bool TryParse(string text, out JsonValue value)
        {
            try
            {
                value = Parse(text);
                return true;
            }
            catch (JsonParseException)
            {
                value = null;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _s;
            public int Pos;

            public Parser(string s)
            {
                _s = s;
            }

            public void SkipWs()
            {
                while (Pos < _s.Length)
                {
                    char c = _s[Pos];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r') break;
                    Pos++;
                }
            }

            private JsonParseException Error(string msg)
            {
                return new JsonParseException(msg, Pos);
            }

            public JsonValue ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Error("nesting deeper than " + MaxDepth);
                if (Pos >= _s.Length) throw Error("unexpected end of input");
                char c = _s[Pos];
                switch (c)
                {
                    case '{':
                        return ParseObject(depth);
                    case '[':
                        return ParseArray(depth);
                    case '"':
                        return JsonValue.From(ParseString());
                    case 't':
                        Expect("true");
                        return JsonValue.True;
                    case 'f':
                        Expect("false");
                        return JsonValue.False;
                    case 'n':
                        Expect("null");
                        return JsonValue.Null;
                    default:
                        if (c == '-' || c >= '0' && c <= '9') return ParseNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, Pos, word, 0, word.Length) != 0) throw Error("invalid literal");
                Pos += word.Length;
            }

            private JsonObject ParseObject(int depth)
            {
                var obj = new JsonObject();
                Pos++;
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == '}')
                {
                    Pos++;
                    return obj;
                }
                while (true)
                {
                    SkipWs();
                    if (Pos >= _s.Length || _s[Pos] != '"') throw Error("expected a string key");
                    int keyPos = Pos;
                    string key = ParseString();
                    if (obj.ContainsKey(key)) throw new JsonParseException("duplicate key \"" + key + "\"", keyPos);
                    SkipWs();
                    if (Pos >= _s.Length || _s[Pos] != ':') throw Error("expected ':'");
                    Pos++;
                    SkipWs();
                    obj.Set(key, ParseValue(depth + 1));
                    SkipWs();
                    if (Pos >= _s.Length) throw Error("unexpected end of input");
                    if (_s[Pos] == ',')
                    {
                        Pos++;
                        continue;
                    }
                    if (_s[Pos] == '}')
                    {
                        Pos++;
                        return obj;
                    }
                    throw Error("expected ',' or '}'");
                }
            }

            private JsonArray ParseArray(int depth)
            {
                var arr = new JsonArray();
                Pos++;
                SkipWs();
                if (Pos < _s.Length && _s[Pos] == ']')
                {
                    Pos++;
                    return arr;
                }
                while (true)
                {
                    SkipWs();
                    arr.Add(ParseValue(depth + 1));
                    SkipWs();
                    if (Pos >= _s.Length) throw Error("unexpected end of input");
                    if (_s[Pos] == ',')
                    {
                        Pos++;
                        continue;
                    }
                    if (_s[Pos] == ']')
                    {
                        Pos++;
                        return arr;
                    }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                Pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (Pos >= _s.Length) throw Error("unterminated string");
                    char c = _s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw new JsonParseException("control character in string", Pos - 1);
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (Pos >= _s.Length) throw Error("unterminated escape");
                    char e = _s[Pos++];
                    switch (e)
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '/':
                            sb.Append('/');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            if (Pos + 4 > _s.Length) throw Error("truncated \\u escape");
                            int code = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int h = Hex(_s[Pos + k]);
                                if (h < 0) throw Error("invalid \\u escape");
                                code = code * 16 + h;
                            }
                            Pos += 4;
                            sb.Append((char)code);
                            break;
                        default:
                            throw new JsonParseException("invalid escape '\\" + e + "'", Pos - 1);
                    }
                }
            }

            private static int Hex(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private JsonValue ParseNumber()
            {
                int start = Pos;
                if (_s[Pos] == '-') Pos++;
                if (Pos >= _s.Length) throw Error("invalid number");
                if (_s[Pos] == '0') Pos++;
                else if (_s[Pos] >= '1' && _s[Pos] <= '9')
                    while (Pos < _s.Length && _s[Pos] >= '0' && _s[Pos] <= '9') Pos++;
                else throw Error("invalid number");
                bool isInt = true;
                if (Pos < _s.Length && _s[Pos] == '.')
                {
                    isInt = false;
                    Pos++;
                    if (!Digits()) throw Error("expected digits after '.'");
                }
                if (Pos < _s.Length && (_s[Pos] == 'e' || _s[Pos] == 'E'))
                {
                    isInt = false;
                    Pos++;
                    if (Pos < _s.Length && (_s[Pos] == '+' || _s[Pos] == '-')) Pos++;
                    if (!Digits()) throw Error("expected exponent digits");
                }
                string lit = _s.Substring(start, Pos - start);
                long l;
                if (isInt && long.TryParse(lit, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l))
                    return JsonValue.From(l);
                double d = double.Parse(lit, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (double.IsInfinity(d)) throw new JsonParseException("number out of range", start);
                return JsonValue.From(d);
            }

            private bool Digits()
            {
                int s = Pos;
                while (Pos < _s.Length && _s[Pos] >= '0' && _s[Pos] <= '9') Pos++;
                return Pos > s;
            }
        }
    }
}
