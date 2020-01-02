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

        public JObject GetJObject(string key,JToken value)
        {
            var obj = new JObject() {{"successful", Successful}, {"msg", Msg}};
            obj.Add(key,value);
            return obj;
        }
        public JObject GetJObject(string key, JToken value,string key2,JToken value2)
        {
            var obj = new JObject() { { "successful", Successful }, { "msg", Msg } };
            obj.Add(key, value);
            obj.Add(key2, value2);
            return obj;
        }
    }
}
