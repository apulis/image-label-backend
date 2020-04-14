using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class MapViewModel
    {
        public float iouThr { get; set; }
        public List<MapDataViewModel> data { get; set; }
        public float mean_AP { get; set; }
    }

    public class MapDataViewModel
    {
        public string category { get; set; }
        public int gt_nums { get; set; }
        public int det_nums { get; set; }
        public float recall { get; set; }
        public float ap { get; set; }
    }
}
