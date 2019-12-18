using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Common.Utils
{
    public class Response
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public JObject Data { get; set; }
        public string JObjectToString()
        {
            var obj = new JObject
            {
                {"Code", Code},
                {"Msg", Msg},
                {"Data", Data}
            };
            return obj.ToString();

        }
    }
}
