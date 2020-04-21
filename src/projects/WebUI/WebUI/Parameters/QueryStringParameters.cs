using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.Parameters
{
    public class QueryStringParameters
    {
        private const int MaxPageSize = 20;

        private int _pageSize = 5;

        public int page { get; set; } = 1;

        public int size
        {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
        private List<int> _category_ids = new List<int>();
        public List<int> category_ids { get=> _category_ids; set=> _category_ids=(value == null)?_category_ids:value; }

        public string image_id { get; set; }
        public float iou_start { get; set; }
        public float iou_end { get; set; }

        public string level { get; set; }
    }
}
