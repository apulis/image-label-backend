using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Extensions
{
    public static class StringExtensions
    {
        public static string FromJSBase64(this string content)
        {
            // convert spaces to pluses and trim base64 spacers
            char[] charDoc = content.Replace(' ', '+').TrimEnd(new char[] { '=' }).ToCharArray();

            StringBuilder docBuilder = new StringBuilder();
            for (int index = 0; index < charDoc.Length; index++)
            {
                if ((index % 78 == 76) && (index < charDoc.Length - 1) && charDoc[index] == '+' && charDoc[index + 1] == '+')
                {
                    index++;
                    continue;
                }
                docBuilder.Append(charDoc[index]);
            }
            // Add padding, if needed--replicates 0-2 equals
            docBuilder.Append(new string('=', (4 - docBuilder.Length % 4) % 4));
            return docBuilder.ToString();
        }

    }
};
