using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Common.Utils
{
    public class Response
    {
        public Response()
        {
            Successful = "true";
            Msg = "";
            Data = null;
        }
        public string Successful { get; set; }
        public string Msg { get; set; }
        public JObject Data { get; set; }
        public string JObjectToString()
        {
            var obj = new JObject
            {
                {"successful", Successful},
                {"msg", Msg},
                {"data", Data}
            };
            return obj.ToString();
        }

        public JObject GetJObject(string key,dynamic value)
        {
            var obj = new JObject() {{"successful", Successful}, {"msg", Msg}};
            obj.Add(key, value!=null?JToken.FromObject(value):null);
            return obj;
        }
        public JObject GetJObject(string key, dynamic value,string key2, dynamic value2)
        {
            var obj = new JObject() { { "successful", Successful }, { "msg", Msg } };
            obj.Add(key, value != null ? JToken.FromObject(value) : null);
            obj.Add(key2, value2 != null ? JToken.FromObject(value2) : null);
            return obj;
        }
        public JObject GetJObject(string key, dynamic value, string key2, dynamic value2, string key3, dynamic value3)
        {
            var obj = new JObject() { { "successful", Successful }, { "msg", Msg } };
            obj.Add(key, value != null ? JToken.FromObject(value) : null);
            obj.Add(key2, value2 != null ? JToken.FromObject(value2) : null);
            obj.Add(key3, value3 != null ? JToken.FromObject(value3) : null);
            return obj;
        }
    }
}
