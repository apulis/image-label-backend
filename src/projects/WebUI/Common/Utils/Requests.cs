using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utils
{
    public class Requests
    {
        public static async Task<Stream> GetStream(string _url, Dictionary<string, string> headerDictionary)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);    //创建一个请求示例 
            foreach (var one in headerDictionary)
            {
                request.Headers[one.Key] = one.Value;
            }
            var response = await request.GetResponseAsync();  //获取响应，即发送请求
            Stream responseStream = response.GetResponseStream();
            return responseStream;
        }
        public static async Task<string>  Get(string _url, Dictionary<string, string> headerDictionary)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);    //创建一个请求示例 
            foreach (var one in headerDictionary)
            {
                request.Headers[one.Key] = one.Value;
            }
            var response = await request.GetResponseAsync();  //获取响应，即发送请求
            Stream responseStream = response.GetResponseStream();
            StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8);
            string str = streamReader.ReadToEnd();
            return str;
        }
        public static string Post(string url, Dictionary<string, string> bodyDictionary)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            #region 添加Post 参数
            StringBuilder builder = new StringBuilder();
            int i = 0;
            foreach (var item in bodyDictionary)
            {
                if (i > 0)
                    builder.Append("&");
                builder.AppendFormat("{0}={1}", item.Key, item.Value);
                i++;
            }
            byte[] data = Encoding.UTF8.GetBytes(builder.ToString());
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }
            #endregion
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //获取响应内容
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
        public static async Task<string> Post(string url, Stream sourceStream, Dictionary<string, string> headerDictionary)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.ContentLength = sourceStream.Length;
            foreach (var one in headerDictionary)
            {
                req.Headers[one.Key] = one.Value;
            }
            using (Stream reqStream = req.GetRequestStream())
            {
                await sourceStream.CopyToAsync(reqStream);
                reqStream.Close();
            }
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //获取响应内容
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
        public static async Task<string> Put(string url,Dictionary<string, string> headerDictionary)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "PUT";
            foreach (var one in headerDictionary)
            {
                req.Headers[one.Key] = one.Value;
            }
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //获取响应内容
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
        public static async Task<string> Delete(string url, Stream sourceStream, Dictionary<string, string> headerDictionary)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "DELETE";
            req.ContentType = "application/json";
            req.ContentLength = sourceStream.Length;
            foreach (var one in headerDictionary)
            {
                req.Headers[one.Key] = one.Value;
            }
            using (Stream reqStream = req.GetRequestStream())
            {
                await sourceStream.CopyToAsync(reqStream);
                reqStream.Close();
            }
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //获取响应内容
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
    }

}
