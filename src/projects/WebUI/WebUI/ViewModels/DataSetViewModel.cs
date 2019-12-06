﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.ViewModels
{
    public class DataSetViewModel
    {
        public string GUid { get; set; }
        public string Name { get; set; }
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