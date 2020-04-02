using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Common.Utils
{
    public class FileOps
    {
        public static string GetFileContentType(string path)
        {
            var type = Path.GetExtension(path);
            string contentType;
            if (type == ".json")
            {
                contentType = "text/plain";
            }
            else if (type == ".jpg")
            {
                contentType = "image/jpeg";
            }
            else
            {
                contentType = "application/octet-stream";
            }

            return contentType;
        }
    }
}
