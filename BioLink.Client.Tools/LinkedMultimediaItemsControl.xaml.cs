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
using BioLink.Client.Extensibility;
using BioLink.Client.Utilities;
using BioLink.Data.Model;
using BioLink.Data;
using System.Collections.ObjectModel;

namespace BioLink.Client.Tools {
    /// <summary>
    /// Interaction logic for LinkedMultimediaItemsControl.xaml
    /// </summary>
    public partial class LinkedMultimediaItemsControl : UserControl, IIdentifiableContent {

        public LinkedMultimediaItemsControl(MultimediaLinkViewModel viewModel) {
            InitializeComponent();
            this.DataContext = viewModel;

            this.MultimediaID = viewModel.MultimediaID;            
            var service = new SupportService(User);

            var mm = service.GetMultimedia(MultimediaID);

            var items = service.ListItemsLinkedToMultimedia(MultimediaID);
            var model = new ObservableCollection<ViewModelBase>();

            foreach (MultimediaLinkedItem item in items) {
                if ( !string.IsNullOrWhiteSpace(item.CategoryName)) {
                    LookupType t;
                    if (Enum.TryParse<LookupType>(item.CategoryName, out t)) {
                        var vm = PluginManager.Instance.GetViewModel(t, item.IntraCatID);
                        if (vm!= null) {
                            model.Add(vm);
                        }
                    }
                }
            }

            lvw.MouseRightButtonUp += new MouseButtonEventHandler(lvw_MouseRightButtonUp);

            lvw.ItemsSource = model;
        }

        void lvw_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            var selected = lvw.SelectedItem as ViewModelBase;
            if (selected != null) {
                var list = new List<ViewModelBase>();
                list.Add(selected);
                var commands = PluginManager.Instance.SolicitCommandsForObjects(list);
                if (commands.Count > 0) {
                    var builder = new ContextMenuBuilder(null);
                    foreach (Command loopvar in commands) {
                        Command cmd = loopvar; // include this in the closure scope, loopvar is outside, hence will always point to the last item...
                        if (cmd is CommandSeparator) {
                            builder.Separator();
                        } else {
                            builder.New(cmd.Caption).Handler(() => { cmd.CommandAction(selected); }).End();   
                        }                        
                    }
                    lvw.ContextMenu = builder.ContextMenu;
                }
            }
        }

        public User User { get { return PluginManager.Instance.User; } }

        public int MultimediaID { get; private set; }

        public string ContentIdentifier {
            get { return "ItemsForMultimedia:" + MultimediaID; }
        }

        public void RefreshContent() {
            throw new NotImplementedException();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) {
            var window = this.FindParentWindow();
            if (window != null) {
                window.Close();
            }
        }

    }
}
