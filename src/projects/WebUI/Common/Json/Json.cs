using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;

namespace Utils.Json
{
    public class Json
    {
        public static JToken GetJToken(string entryname, JToken token)
        {
            if (String.IsNullOrEmpty(entryname))
                return null;
            else
            {
                if (Object.ReferenceEquals(token, null))
                    return null; 
                JObject jobj = token as JObject;
                if (!Object.ReferenceEquals(jobj, null))
                {
                    foreach (var pair in jobj)
                    {
                        string name = pair.Key;
                        JToken value = pair.Value;
                        if (name.Length <= entryname.Length && String.CompareOrdinal(name, entryname.Substring(0, name.Length)) == 0)
                        {
                            if(entryname.Length > name.Length&& entryname.Substring(name.Length, 1) == "_")
                            {
                                return GetJToken(entryname.Substring(name.Length + 1), value);
                            }
                            return value;

                        }
                    }
                    return null;
                }
                JArray jarr = token as JArray;
                if (!Object.ReferenceEquals(jarr, null))
                {
                    foreach (var item in jarr)
                    {
                        var ret = GetJToken(entryname, item);
                        if (!Object.ReferenceEquals(ret, null))
                            return ret;
                    }
                    return null;
                }
                JProperty jprop = token as JProperty;
                if (!Object.ReferenceEquals(jprop, null))
                {
                    string name = jprop.Name;
                    JToken value = jprop.Value;
                    if (name.Length <= entryname.Length && String.Compare(name, entryname.Substring(0, name.Length), StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        if (entryname.Length > name.Length)
                            return GetJToken(entryname.Substring(name.Length + 1), value);
                        else
                            return value;
                    }
                    return null;
                }
                JValue jval = token as JValue;
                if (!Object.ReferenceEquals(jval, null))
                {
                    JToken value = jval;
                    return null;
                }

            }
            return null;
        }

        public static JToken GetJToken(string[] entrynamelist, JToken token)
        {
            var re = token;
            foreach (var entryname in entrynamelist)
            {
                re = GetJToken(entryname, re);
                if (re == null)
                {
                    return null;
                }
            }
            return re;
        }

        public static string GetString(string entryname, JToken token, string def = "")
        {
            var obj = GetJToken(entryname, token);
            if (Object.ReferenceEquals(obj, null))
                return def;
            else
                return obj.ToString();
        }

        public static string[] GetStrings(string entryname, JToken token )
        {
            var arr = GetJToken(entryname, token) as JArray;
            return GetStrings( arr );
        }

        public static string[] GetStrings( JArray arr )
        {
            if (Object.ReferenceEquals(arr, null))
                return new string[] {};
            else
            {
                var lst = new List<string>(); 
                foreach ( var oneitem in arr )
                {
                    var str = oneitem.ToString(); 
                    if ( !String.IsNullOrEmpty(str))
                        lst.Add( str );
                }
                return lst.ToArray(); 
            }
        }

        public static bool GetBool( string entryname, JToken token, bool def = true )
        {
            var obj = GetJToken(entryname, token); 
            if (Object.ReferenceEquals(obj, null))
                return def;
            else
            {
                try
                {
                    return obj.ToObject<bool>();
                }
                catch
                {
                    return def; 
                }
            }
        }

        public static T GetType<T>(string entryname, JToken token, T def)
        {
            var obj = GetJToken(entryname, token);
            if (Object.ReferenceEquals(obj, null))
                return def;
            else
            {
                try
                {
                    return obj.ToObject<T>();
                }
                catch
                {
                    return def;
                }
            }
        }

        public static DateTime GetDateTime(string entryname, JToken token, DateTime def )
        {
            var obj = GetJToken(entryname, token);
            if (Object.ReferenceEquals(obj, null))
                return def;
            else
            {
                try
                {
                    return obj.ToObject<DateTime>();
                }
                catch
                {
                    return def;
                }
            }
        }
        public static JObject Read(Stream stream)
        {
            var text = "";
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }
            return JObject.Parse(text);
        }

        public static JObject Read(String filename)
        {
            try { 
                using (var file = new FileStream(filename, FileMode.Open))
                {
                    return JsonUtils.Read(file);
                }
            } catch 
            {
                return null; 
            }
        }

        public static JObject TryParse(String content )
        {
            try {
                if ( String.IsNullOrEmpty(content))
                    return new JObject(); 
                else {
                    return JObject.Parse(content);
                }
            } catch {
                return new JObject(); 
            }
        }

        /// <summary>
        /// Has entryname contains some of the token value
        /// </summary>
        /// <param name="entryname"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static JToken PartialMatch(string entryname, JToken token)
        {
            JObject jobj = token as JObject;
            JArray jarr = token as JArray;
            if (!Object.ReferenceEquals(jobj, null))
            {
                foreach (var pair in jobj)
                {
                    string name = pair.Key;
                    JToken value = pair.Value;
                    if (entryname.IndexOf(name, 0, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return value;
                    }
                }
                return null;
            }
            else if (!Object.ReferenceEquals(jarr, null))
            {
                foreach (var item in jarr)
                {
                    var ret = PartialMatch(entryname, item);
                    if (!Object.ReferenceEquals(ret, null))
                        return ret;
                }
                return null;
            }
            else
            {
                var name = token.ToString();
                if (entryname.IndexOf(name, 0, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return token;
                }
                else
                    return null;
            }

        }

        public static IEnumerable<KeyValuePair<string, JToken>> Iterate(JToken token)
        {
            JObject jobj = token as JObject;
            JArray jarr = token as JArray;
            if (!Object.ReferenceEquals(jobj, null))
            {
                foreach (var pair in jobj)
                {
                    yield return pair;
                }
            }
            else if (!Object.ReferenceEquals(jarr, null))
            {
                foreach (var item in jarr)
                {
                    yield return new KeyValuePair<string, JToken>(item.ToString(), item);
                }
            }
            else
            {
                if (Object.ReferenceEquals(token, null))
                {
                }
                else
                { 
                    yield return new KeyValuePair<string, JToken>(token.ToString(), token);
                }
            }
        }

        private static void EmitEmptyCells( int level, StringBuilder buf )
        {
            buf.Append("<tr>");
            for (int i = 0; i < level; i++)
                // Empty table cell
                buf.Append("<td></td>");
        }

        private static void EmitHtmlRecursive(JToken token, int level, StringBuilder buf, ref int level_max )
        {
            if ( level_max < level)
                level_max = level;
            JObject jobj = token as JObject;
            JArray jarr = token as JArray;
            var jprop = token as JProperty;
            if (!Object.ReferenceEquals(jobj, null))
            {
                foreach (var pair in jobj)
                {
                    EmitEmptyCells(level, buf);
                    buf.Append($"<td>{pair.Key}<td> </tr>");
                    EmitHtmlRecursive(pair.Value, level + 1, buf, ref level_max);
                }
            }
            else if (!Object.ReferenceEquals(jarr, null))
            {
                foreach (var item in jarr)
                {
                    EmitHtmlRecursive(item, level, buf, ref level_max);
                }
            }
            else if (!Object.ReferenceEquals(jprop, null))
            {
                EmitEmptyCells(level, buf);
                buf.Append($"<td>{jprop.Name}<td> </tr>");
                EmitHtmlRecursive(jprop.Value, level + 1, buf, ref level_max);
            }
            else
            {
                var jval = token as JValue;
                EmitEmptyCells(level, buf);
                buf.Append($"<td>{jval}<td> </tr>");
            }
        }

        /// <summary>
        /// Emit a HTML table that shows Json content
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static void EmitHtml(JToken token, out string head_string, out string body_string)
        {
            StringBuilder head = new StringBuilder();
            StringBuilder body = new StringBuilder();
            int level_max=0;
            EmitHtmlRecursive(token, 0, body, ref level_max);
            head.Append("<tr>");
            for (int i = 0; i < level_max; i++)
            {
                if ( i==0 )
                    head.Append("<th>Lv=1</th>");
                else
                    head.Append($"<th>Lv={i+1}</th>");
            }
            head.Append("</tr>");
            head_string = head.ToString();
            body_string = body.ToString(); 
        }

        public static bool ContainsKey(string entryname, JToken token)
        {
            if (Object.ReferenceEquals(token, null))
                return false; 
            JObject jobj = token as JObject;
            JArray jarr = token as JArray;
            if (!Object.ReferenceEquals(jobj, null))
            {
                JToken val;
                return jobj.TryGetValue(entryname, StringComparison.OrdinalIgnoreCase, out val);
            }
            else if (!Object.ReferenceEquals(jarr, null))
            {
                foreach (var item in jarr)
                {
                    if (ContainsKey(entryname, item))
                        return true;
                }
                return false;
            }
            else
            {
                var name = token.ToString();
                return String.Compare(name, entryname, true) == 0;
            }
        }

        public static bool AddValueToJObject(string[] entryNameList, JObject obj, JToken value)
        {
            int len = entryNameList.Length;
            int index = 0;
            JObject currenJObject = obj;
            foreach (var entryName in entryNameList)
            {
                if (Json.GetJToken(entryName, currenJObject) == null)
                {
                    var i = len - 1;
                    JToken newObj = value;
                    while (i > index)
                    {
                        newObj = new JObject() { { entryNameList[i], newObj } };
                        i -= 1;
                    }
                    currenJObject.Add(entryName, newObj);
                    return true;
                }
                currenJObject = Json.GetJToken(entryName, currenJObject) as JObject;
                index += 1;
            }
            return false;
        }
        public static bool AddValueToJArray(string[] entryNameList, JObject obj, string value)
        {
            int len = entryNameList.Length;
            int index = 0;
            var currenJObject = obj;
            foreach (var entryName in entryNameList)
            {
                var currentObj = Json.GetJToken(entryName, currenJObject);
                if (currentObj == null)
                {
                    var i = len - 1;
                    JToken newObj = new JArray() { value };
                    while (i > index)
                    {
                        newObj = new JObject() { { entryNameList[i], newObj } };
                        i -= 1;
                    }
                    currenJObject.Add(entryName, newObj);
                    return true;
                }
                if (index == len - 1)
                {
                    var array = currentObj as JArray;
                    if (!Json.ContainsKey(value, array))
                    {
                        array.Add(value);
                        return true;
                    }
                }
                else
                {
                    currenJObject = currentObj as JObject;
                }
                index += 1;
            }
            return false;
        }
    }

    public class JsonUtils : Utils.Json.Json
    {
    }

    public static class StringExtension 
    {
        public static String Combine( this String cur, String sep, String join )
        {
            if ( String.IsNullOrEmpty( join ))
            {
                return cur; 
            } else {
                if ( String.IsNullOrEmpty(cur))
                {
                    return join;
                } else {
                    return cur + sep + join; 
                }
            } 
        }
    }

    public static class JObjectExtension
    {
        public static JObject Add( this JObject obj, double value, string description, string sep )
        {
            var curValue = JsonUtils.GetType<double>("value", obj, 0.0);
            curValue += value;
            if ( curValue != 0.0 )
            {
                obj["value"] = curValue;
            } else if ( value != 0.0 ){
                obj.Remove("value"); 
            }
            var curDescription = JsonUtils.GetString("description", obj);
            curDescription = curDescription.Combine(sep, description); 
            if ( !String.IsNullOrEmpty( curDescription) )
            {
                obj["description"] = curDescription; 
            }
            return obj; 
        }

        /// <summary>
        /// The key object will have two component: value = ..., and description == ...
        /// This function will update both the value and description, if any. 
        /// </summary>
        /// <returns>The added object.</returns>
        /// <param name="obj">Object.</param>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        /// <param name="description">Description.</param>
        /// <param name="sep">seperator, default to " + ".</param>
        public static JObject Add(this JObject obj, string key, double value, string description, string sep = " + " )
        {
            if ( value == 0.0 && String.IsNullOrEmpty(description))
            {
                // Nothing to change. 
                return obj; 
            }
            var keyObj = JsonUtils.GetJToken(key, obj) as JObject; 
            if ( Object.ReferenceEquals(keyObj,null) )
            {
                keyObj = new JObject();
                obj[key] = keyObj;
            }
            keyObj.Add(value, description, sep);
            return obj;
        }

        public static JObject DeepCloneJObject( this JObject obj)
        {
            var newObj = obj.DeepClone() as JObject; 
            if ( Object.ReferenceEquals(newObj,null))
            {
                newObj = new JObject(); 
            }
            return newObj;
        }
    }
}
