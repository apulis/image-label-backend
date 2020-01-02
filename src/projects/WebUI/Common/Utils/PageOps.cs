using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Newtonsoft.Json.Linq;

namespace Common.Utils
{
    public class PageOps
    {
        public static List<T> GetPageRange<T>(List<T> list,int page,int size,int total)
        {
            if (page > 0 && size > 0)
            {
                var start = (page - 1) * size;
                start = start > total ? total : start;
                var end = page * size;
                end = end > total ? total : end;
                var cnt = end - start;
                return list.GetRange(start, cnt);
            }
            return list;
        }
    }
}
