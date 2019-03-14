using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;
using System.Drawing;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Common.Utils
{
    public class ImageOps
    {
        public static Image FromBase64( string data, ILogger _logger )
        {
            var dataLen = data.Length; 
            var bytearr = Convert.FromBase64String(data);
            using (var inp = new MemoryStream(bytearr))
            {
                var img = Image.FromStream(inp);
                return img; 
            }
            return null; 
        }

        public static string FromJSBase64( string content )
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

    public static class ImageExtensions
    {
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        public static byte[] ToJPEG(this Image img, long quality=95L )
        {
            // Get an ImageCodecInfo object that represents the JPEG codec.
            var jpgImageCodecInfo = GetEncoderInfo("image/jpeg");

            // Create an Encoder object based on the GUID

            // for the Quality parameter category.
            var jpgEncoder = System.Drawing.Imaging.Encoder.Quality;

            // Create an EncoderParameters object.

            // An EncoderParameters object has an array of EncoderParameter

            // objects. In this case, there is only one

            // EncoderParameter object in the array.
            var jpgEncoderParameters = new EncoderParameters(1);

            // Save the bitmap as a JPEG file with quality level 25.
            var jpgEncoderParameter = new EncoderParameter(jpgEncoder, quality);
            jpgEncoderParameters.Param[0] = jpgEncoderParameter;
            using (var byt = new MemoryStream())
            {
                img.Save(byt, jpgImageCodecInfo, jpgEncoderParameters);
                return byt.GetBuffer(); 
            }
        }
    }
}
