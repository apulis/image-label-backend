using System.Collections.Generic;
using WebUI.ViewModels;

namespace WebUI.Models
{
    public class DataSetModel
    {
        public string GUid { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string Type { get; set; }
        public string Role { get; set; }
        public string dataSetId { get; set; }
        public List<UserEmailViewModel> Users { get; set; }
        public DataSetType dataSetType { get; set; }

        public string AddUser { get; set; }

        public enum DataSetType
        {
            Vedio = 1,
            Image = 2
        }
    }
}
