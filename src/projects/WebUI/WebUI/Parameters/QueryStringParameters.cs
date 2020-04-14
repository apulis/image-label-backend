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
        public List<int> category_ids { get; set; }

        public string image_id { get; set; }
        public float iou_start { get; set; }
        public float iou_end { get; set; }
    }
}
