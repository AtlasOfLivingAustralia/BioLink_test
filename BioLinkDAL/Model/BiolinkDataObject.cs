﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioLink.Data.Model {

    public abstract class BiolinkDataObject {
        public Nullable<Guid> GUID { get; set; }
    }

}