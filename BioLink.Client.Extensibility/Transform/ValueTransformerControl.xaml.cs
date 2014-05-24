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

namespace BioLink.Client.Extensibility {
    /// <summary>
    /// Interaction logic for ValueTransformerControl.xaml
    /// </summary>
    public partial class ValueTransformerControl : UserControl {

        public ValueTransformerControl() {
            InitializeComponent();            
        }

        public ValueTransformerControl(IValueTransformer transformer) {
            InitializeComponent();
            this.DataContext = transformer;            
        }

    }
}
