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
        public string Name { get; set; }
        public string Info { get; set; }
        public string Type { get; set; }
        public string Role { get; set; }
        public string dataSetBindId { get; set; }
        public string dataSetPath { get; set; }
        public List<AddLabelViewModel> Labels { get; set; }
    }
}
