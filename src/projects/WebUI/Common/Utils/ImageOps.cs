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
