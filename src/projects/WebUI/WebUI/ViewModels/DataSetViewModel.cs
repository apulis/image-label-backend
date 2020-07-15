using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WebUI.ViewModels
{
    public class DatasetViewModel
    {
        public string dataSetId { get; set; }
        public string name { get; set; }
        public string info { get; set; }
        public string type { get; set; }
        public string role { get; set; }
        public string dataSetBindId { get; set; }
        public string dataSetPath { get; set; }
        public string convertStatus { get; set; }
        public string convertOutPath { get; set; }
        public List<AddLabelViewModel> labels { get; set; }
    }
}
