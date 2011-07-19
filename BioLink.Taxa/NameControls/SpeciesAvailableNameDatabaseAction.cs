﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioLink.Client.Extensibility;
using BioLink.Data;
using BioLink.Data.Model;
using BioLink.Client.Utilities;

namespace BioLink.Client.Taxa {

    public class UpdateSanDatabaseAction : GenericDatabaseCommand<SpeciesAvailableName> {

        public UpdateSanDatabaseAction(SpeciesAvailableName name)
            : base(name) {
        }

        protected override void ProcessImpl(User user) {
            var service = new TaxaService(user);
            service.InsertOrUpdateSpeciesAvailableName(Model);
        }
    }

    public class UpdateSANTypeDataAction : GenericDatabaseCommand<SANTypeData> {

        public UpdateSANTypeDataAction(SANTypeData model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            Debug.Assert(Model.SANTypeDataID >= 0);
            var service = new TaxaService(user);
            service.UpdateSANTypeData(Model);
        }
    }

    public class InsertSANTypeDataAction : GenericDatabaseCommand<SANTypeData> {

        public InsertSANTypeDataAction(SANTypeData model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            Debug.Assert(Model.SANTypeDataID < 0);
            var service = new TaxaService(user);
            service.InsertSANTypeData(Model);
        }
    }

    public class DeleteSANTypeDataAction : GenericDatabaseCommand<SANTypeData> {

        public DeleteSANTypeDataAction(SANTypeData model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            Debug.Assert(Model.SANTypeDataID >= 0);
            var service = new TaxaService(user);
            service.DeleteSANTypeData(Model.SANTypeDataID);
        }
    }

}
