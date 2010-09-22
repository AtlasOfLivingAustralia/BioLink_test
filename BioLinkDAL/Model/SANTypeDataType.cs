﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioLink.Data.Model {

    public class SANTypeDataType : GUIDObject {
        public string PrimaryType { get; set; }
        public IEnumerable<String> SecondaryTypes { get; set; }
    }
}