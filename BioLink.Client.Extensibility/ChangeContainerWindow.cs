﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using BioLink.Data;
using BioLink.Client.Utilities;
using System.Collections.ObjectModel;

namespace BioLink.Client.Extensibility {

    public class ChangeContainerWindow : Window, IChangeContainer  {

        private ChangeContainerImpl _impl;
        private User _user;

        #region designer ctor
        public ChangeContainerWindow() {
        }
        #endregion

        public ChangeContainerWindow(User user) {
            this.User = user;
            _impl = new ChangeContainerImpl(user);
            _impl.ChangeRegistered += new PendingChangedRegisteredHandler(_impl_ChangeRegistered);
            _impl.ChangesCommitted += new PendingChangesCommittedHandler(_impl_ChangesCommitted);
        }

        void _impl_ChangesCommitted(object sender) {
            if (this.ChangesCommitted != null) {
                ChangesCommitted(sender);
            }
        }

        void _impl_ChangeRegistered(object sender, object action) {
            if (this.ChangeRegistered != null) {
                ChangeRegistered(sender, action);
            }
        }

        public bool HasPendingChanges {
            get { return _impl.HasPendingChanges; }
        }

        public void RegisterPendingChange(DatabaseCommand action, object contributer) {
            _impl.RegisterPendingChange(action, contributer);
        }

        public bool RegisterUniquePendingChange(DatabaseCommand action, object contributer) {
            return _impl.RegisterUniquePendingChange(action, contributer);
        }

        public void RegisterPendingChanges(List<DatabaseCommand> actions, object contributer) {
            _impl.RegisterPendingChanges(actions, contributer);
        }

        public void ClearPendingChanges() {
            _impl.ClearPendingChanges();
        }

        public void ClearMatchingPendingChanges(Predicate<DatabaseCommand> predicate) {
            _impl.ClearMatchingPendingChanges(predicate);
        }


        public void CommitPendingChanges(Action successAction = null) {
            _impl.CommitPendingChanges(successAction);
        }

        public ObservableCollection<DatabaseCommand> PendingChanges {
            get { return _impl.PendingChanges; }
        }

        public User User {
            get { return _user; }
            protected set {
                _user = value;
                _impl = new ChangeContainerImpl(value);
                _impl.ChangeRegistered += new PendingChangedRegisteredHandler(_impl_ChangeRegistered);
                _impl.ChangesCommitted += new PendingChangesCommittedHandler(_impl_ChangesCommitted);
            }
        }

        public event PendingChangesCommittedHandler ChangesCommitted;

        public event PendingChangedRegisteredHandler ChangeRegistered;

    }

    public class ChangeContainerImpl : IChangeContainer {

        private ObservableCollection<DatabaseCommand> _pendingChanges = new ObservableCollection<DatabaseCommand>();

        private List<IChangeContainerObserver> _observers = new List<IChangeContainerObserver>();

        public ChangeContainerImpl(User user) {
            this.User = user;
        }

        public bool HasPendingChanges {
            get { return _pendingChanges != null && _pendingChanges.Count > 0; }
        }

        public void RegisterPendingChange(DatabaseCommand action, object contributer) {
            if (action != null) {
                _pendingChanges.Add(action);
                if (contributer is IChangeContainerObserver && !_observers.Contains(contributer)) {
                    _observers.Add(contributer as IChangeContainerObserver);
                }
                if (ChangeRegistered != null) {
                    ChangeRegistered(this, action);
                }
            }
        }

        public bool RegisterUniquePendingChange(DatabaseCommand action, object contributer) {
            foreach (DatabaseCommand existingaction in _pendingChanges) {
                if (existingaction.Equals(action)) {
                    return false;
                }
            }
            RegisterPendingChange(action, contributer);
            return true;
        }

        public void RegisterPendingChanges(List<DatabaseCommand> actions, object contributer) {
            foreach (DatabaseCommand action in actions) {
                RegisterPendingChange(action, contributer);
            }
        }

        public void ClearPendingChanges() {
            _pendingChanges.Clear();
        }

        public void ClearMatchingPendingChanges(Predicate<DatabaseCommand> predicate) {
            var purgeList = new List<DatabaseCommand>();
            // Build a list of the database actions that need to be removed...
            _pendingChanges.ForEach(action => {
                if (predicate(action)) {
                    purgeList.Add(action);
                }
            });

            // and remove them
            purgeList.ForEach(action => {
                _pendingChanges.Remove(action);
            });
        }


        public void CommitPendingChanges(Action successAction = null) {

            if (User == null) {
                throw new Exception("User object has not been set on the Control Host Window");
            }
#if DEBUG
            Logger.Debug("About to commit the following changes:");
            foreach (DatabaseCommand action in _pendingChanges) {
                Logger.Debug("{0}", action);
            }
#endif


            // First validate each action...Actions can produce messages if they are not valid.
            var messageList = new List<string>();
            foreach (DatabaseCommand action in _pendingChanges) {
                var messages = action.Validate();
                if (messages != null && messages.Count > 0) {
                    messageList.AddRange(messages);
                }
            }

            if (messageList.Count > 0) {
                ErrorMessage.Show("One or more validation errors occured:\n\n{0}\n\nOperation aborted.", messageList.Join("\n\n"));
                return;
            }

            // It may be that this control is aggregated as part of a larger control. This means that, come save time, there
            // may already be a transaction pending. If so, don'note create a new one, just piggy back on the existing
            bool commitTrans = false;  // flag to let us know if we are responsible for the transaction...

            if (!User.InTransaction) {
                User.BeginTransaction();
                commitTrans = true;
            }
            try {
                foreach (DatabaseCommand action in _pendingChanges) {
                    action.Process(User);
                }

                if (commitTrans) {
                    User.CommitTransaction();
                }

                if (successAction != null) {
                    successAction();
                }

                foreach (IChangeContainerObserver observer in _observers) {
                    observer.OnChangesCommitted();
                }

                if (ChangesCommitted != null) {
                    ChangesCommitted(this);
                }

                _observers.Clear();
                _pendingChanges.Clear();

                // Reload the pinboard...
                JobExecutor.QueueJob(() => {
                    PluginManager.Instance.RefreshPinBoard();
                });

            } catch (Exception ex) {
                if (commitTrans) {
                    User.RollbackTransaction();
                }
                GlobalExceptionHandler.Handle(ex);
            }
        }

        public ObservableCollection<DatabaseCommand> PendingChanges {
            get { return _pendingChanges; }
        }

        public User User { get; protected set; }

        public event PendingChangesCommittedHandler ChangesCommitted;

        public event PendingChangedRegisteredHandler ChangeRegistered;
    }
}
