﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioLink.Data;
using BioLink.Data.Model;

namespace BioLink.Client.Extensibility {

    public class InsertAssociateAction : GenericDatabaseAction<Associate> {

        public InsertAssociateAction(Associate model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            var service =new SupportService(user);
            Model.AssociateID = service.InsertAssociate(Model);
        }

    }

    public class UpdateAssociateAction : GenericDatabaseAction<Associate> {
        public UpdateAssociateAction(Associate model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            var service = new SupportService(user);
            service.UpdateAssociate(Model);
        }
    }

    public class DeleteAssociateAction : GenericDatabaseAction<Associate> {
        public DeleteAssociateAction(Associate model)
            : base(model) {
        }

        protected override void ProcessImpl(User user) {
            var service = new SupportService(user);
            service.DeleteAssociate(Model.AssociateID);
        }
    }
}