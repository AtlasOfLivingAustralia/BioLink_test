﻿/*******************************************************************************
 * Copyright (C) 2011 Atlas of Living Australia
 * All Rights Reserved.
 * 
 * The contents of this file are subject to the Mozilla Public
 * License Version 1.1 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of
 * the License at http://www.mozilla.org/MPL/
 * 
 * Software distributed under the License is distributed on an "AS
 * IS" basis, WITHOUT WARRANTY OF ANY KIND, either express or
 * implied. See the License for the specific language governing
 * rights and limitations under the License.
 ******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BioLink.Data.Model;
using BioLink.Client.Utilities;

namespace BioLink.Client.Extensibility {
    /// <summary>
    /// Interaction logic for DuplicateItemOptions.xaml
    /// </summary>
    public partial class DuplicateItemOptions : Window {

        #region Designer Constructor
        public DuplicateItemOptions() {
            InitializeComponent();
        }
        #endregion

        public DuplicateItemOptions(Multimedia duplicate, int sizeInBytes, Boolean managerMode = false) {
            InitializeComponent();
            this.DuplicateItem = duplicate;
            lblDescription.Content = "There already exists a multimedia item with the name and size ('" + duplicate.Name + "', " +  ByteLengthConverter.FormatBytes(sizeInBytes) + ").";
            if (managerMode) {
                optContinue.IsChecked = true;
                optLinkToExisting.IsEnabled = false;
                optLinkToExisting.IsChecked = false;                
            }
        }

        internal Multimedia DuplicateItem { get; private set; }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;

            if (optCancel.IsChecked.ValueOrFalse()) {
                SelectedAction = MultimediaDuplicateAction.Cancel;
            } else if (optContinue.IsChecked.ValueOrFalse()) {
                SelectedAction = MultimediaDuplicateAction.InsertDuplicate;
            } else if (optReplace.IsChecked.ValueOrFalse()) {
                SelectedAction = MultimediaDuplicateAction.ReplaceExisting;
            } else if (optLinkToExisting.IsChecked.ValueOrFalse()) {
                SelectedAction = MultimediaDuplicateAction.UseExisting;
            } else {
                throw new Exception("Unhandled option!");
            }

            this.Close();
        }

        public MultimediaDuplicateAction SelectedAction { get; private set; }

    }
}
