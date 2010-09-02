﻿using System;
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
using BioLink.Data;
using BioLink.Data.Model;


namespace BioLink.Client.Extensibility {
    /// <summary>
    /// Interaction logic for PhraseSelector.xaml
    /// </summary>
    public partial class PhraseSelector : UserControl {

        #region DesignerConstructor
        public PhraseSelector() {
            InitializeComponent();
        }
        #endregion

        public PhraseSelector(User user, String phraseCategory, bool @fixed) {
            InitializeComponent();
            BindUser(user, phraseCategory, @fixed);
        }

        public void BindUser(User user, String phraseCategory, bool @fixed) {
            this.User = user;
            this.PhraseCategory = phraseCategory;
            this.IsFixedPhrase = @fixed;
        }

        private void btn_Click(object sender, RoutedEventArgs e) {
            ShowPhraseSelectorWindow();
        }

        private void ShowPhraseSelectorWindow() {
            PhraseSelectorWindow frm = new PhraseSelectorWindow(this.User, PhraseCategory, IsFixedPhrase);
            if (frm.ShowDialog().GetValueOrDefault(false)) {
                txt.Text = frm.SelectedPhrase.PhraseText;
            };
        }

        protected User User { get; private set; }

        protected string PhraseCategory { get; private set; }

        protected bool IsFixedPhrase { get; private set; }

    }
}