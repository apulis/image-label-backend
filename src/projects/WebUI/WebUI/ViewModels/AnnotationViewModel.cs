using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class AnnotationViewModel
    {
        public List<ImageMetaInfo> images { get; }
        public List<AnnatationInfo> annotations { get; }
    }

    public class ImageMetaInfo
    {
        public int license { get; set; }
        public string file_name { get; set; }
        public string coco_url { get; set; }
        public int height { get; set; }    
        public int width { get; set; }    
        public string date_captured { get; set; }    
        public string flickr_url { get; set; }    
        public int id { get; set; }    
    }

    public class AnnatationInfo
    {
        public List<int> segmentation { get; }
        public int area { get; }
        public int iscrowd { get; }
        public int image_id { get; }
        public List<int> bbox { get; }
        public int category_id { get; }
        public int id { get; }

    }
}
