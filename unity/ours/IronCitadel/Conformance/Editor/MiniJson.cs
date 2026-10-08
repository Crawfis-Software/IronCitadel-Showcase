// IronCitadel conformance kit: a tiny JSON reader and writer, so the kit needs no JSON package.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace IronCitadel.Conformance
{
    /// <summary>An insertion-ordered JSON object, used for every report node.</summary>
    public sealed class JObj : List<KeyValuePair<string, object>>
    {
        public JObj Add(string key, object value)
        {
            base.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public object this[string key]
        {
            get
            {
                foreach (var kv in this) if (kv.Key == key) return kv.Value;
                return null;
            }
        }
    }

    public static class MiniJson
    {
        // ---------- reading ----------

        public static object Parse(string text)
        {
            var p = new Parser(text);
            var v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw new FormatException("JSON: trailing characters at offset " + p.Pos);
            return v;
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string s) { this.s = s; }
            public bool End => i >= s.Length;
            public int Pos => i;

            public void SkipWs()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public object ReadValue()
            {
                SkipWs();
                if (i >= s.Length) throw new FormatException("JSON: unexpected end");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            void Expect(string word)
            {
                if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new FormatException("JSON: bad literal at offset " + i);
                i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; // {
                SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (s[i] != '"') throw new FormatException("JSON: expected a key at offset " + i);
                    string k = ReadString();
                    SkipWs();
                    if (s[i] != ':') throw new FormatException("JSON: expected ':' at offset " + i);
                    i++;
                    d[k] = ReadValue();
                    SkipWs();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("JSON: expected ',' or '}' at offset " + i);
                }
            }

            List<object> ReadArray()
            {
                var l = new List<object>();
                i++; // [
                SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ReadValue());
                    SkipWs();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("JSON: expected ',' or ']' at offset " + i);
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                i++; // opening quote
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
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
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException("JSON: bad escape at offset " + i);
                    }
                }
                throw new FormatException("JSON: unterminated string");
            }

            object ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("JSON: unexpected character '" + s[i] + "' at offset " + i);
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        // ---------- writing ----------

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        static void Indent(StringBuilder sb, int depth)
        {
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        static bool IsScalarList(IList list)
        {
            if (list.Count > 12) return false;
            foreach (var x in list)
                if (!(x == null || x is string || x is bool || IsNumber(x))) return false;
            return true;
        }

        static bool IsNumber(object x) =>
            x is int || x is long || x is float || x is double || x is short || x is byte || x is uint;

        static void WriteValue(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string str: WriteString(sb, str); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case Vector3 p:
                    sb.Append('[').Append(Num(p.x)).Append(", ").Append(Num(p.y)).Append(", ").Append(Num(p.z)).Append(']');
                    return;
                case JObj o:
                    if (o.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    for (int k = 0; k < o.Count; k++)
                    {
                        Indent(sb, depth + 1);
                        WriteString(sb, o[k].Key);
                        sb.Append(": ");
                        WriteValue(sb, o[k].Value, depth + 1);
                        if (k < o.Count - 1) sb.Append(',');
                    }
                    Indent(sb, depth);
                    sb.Append('}');
                    return;
                case IDictionary dict:
                {
                    var o2 = new JObj();
                    foreach (DictionaryEntry e in dict) o2.Add(Convert.ToString(e.Key, CultureInfo.InvariantCulture), e.Value);
                    WriteValue(sb, o2, depth);
                    return;
                }
                case IList list:
                    if (list.Count == 0) { sb.Append("[]"); return; }
                    if (IsScalarList(list))
                    {
                        sb.Append('[');
                        for (int k = 0; k < list.Count; k++)
                        {
                            if (k > 0) sb.Append(", ");
                            WriteValue(sb, list[k], depth + 1);
                        }
                        sb.Append(']');
                        return;
                    }
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++)
                    {
                        Indent(sb, depth + 1);
                        WriteValue(sb, list[k], depth + 1);
                        if (k < list.Count - 1) sb.Append(',');
                    }
                    Indent(sb, depth);
                    sb.Append(']');
                    return;
            }
            if (IsNumber(v))
            {
                sb.Append(Num(Convert.ToDouble(v, CultureInfo.InvariantCulture)));
                return;
            }
            WriteString(sb, v.ToString());
        }

        public static string Num(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
            return Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture);
        }

        static void WriteString(StringBuilder sb, string str)
        {
            sb.Append('"');
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
