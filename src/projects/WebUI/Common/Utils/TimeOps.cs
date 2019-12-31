using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Utils
{
    public class TimeOps
    {
        public static string GetCurrentTimeStamp()
        {
            TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }
    }
}
