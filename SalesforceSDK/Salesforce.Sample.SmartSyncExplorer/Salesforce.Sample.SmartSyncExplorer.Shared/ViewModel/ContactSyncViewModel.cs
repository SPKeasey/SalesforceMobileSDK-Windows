﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Appointments.AppointmentsProvider;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using Salesforce.Sample.SmartSyncExplorer.Annotations;
using Salesforce.Sample.SmartSyncExplorer.utilities;
using Salesforce.SDK.Auth;
using Salesforce.SDK.SmartStore.Store;
using Salesforce.SDK.SmartSync.Manager;
using Salesforce.SDK.SmartSync.Model;
using Salesforce.SDK.SmartSync.Util;

namespace Salesforce.Sample.SmartSyncExplorer.ViewModel
{
    public sealed class ContactSyncViewModel : INotifyPropertyChanged
    {
        public const string ContactSoup = "contacts";
        public const int Limit = 10000;
        private static readonly object _syncLock = new object();

        private static readonly IndexSpec[] ContactsIndexSpec =
        {
            new IndexSpec("Id", SmartStoreType.SmartString),
            new IndexSpec("FirstName", SmartStoreType.SmartString),
            new IndexSpec("LastName", SmartStoreType.SmartString),
            new IndexSpec(SyncManager.LocallyCreated, SmartStoreType.SmartString),
            new IndexSpec(SyncManager.LocallyUpdated, SmartStoreType.SmartString),
            new IndexSpec(SyncManager.LocallyDeleted, SmartStoreType.SmartString),
            new IndexSpec(SyncManager.Local, SmartStoreType.SmartString)
        };

        private readonly SmartStore _store;
        private readonly SyncManager _syncManager;

        public ContactSyncViewModel()
        {
            Account account = AccountManager.GetAccount();
            if (account == null) return;
            _store = new SmartStore();
            SmartStore.CreateMetaTables();
            _syncManager = SyncManager.GetInstance(account);
            Contacts = new SortedObservableCollection<ContactObject>();
            FilteredContacts = new SortedObservableCollection<ContactObject>();
        }

        private SortedObservableCollection<ContactObject> _contacts;
        private SortedObservableCollection<ContactObject> _filteredContacts;

        public SortedObservableCollection<ContactObject> Contacts
        {
            get
            {
                return _contacts;
            }
            private set
            {
                _contacts = value;
                OnPropertyChanged();
            }
        }

        private string _filter;
        public string Filter
        {
            get
            {
                return _filter ?? String.Empty;
            }
            set
            {
                _filter = value;
                RunFilter();
            }
        }

        public SortedObservableCollection<ContactObject> FilteredContacts
        {
            get
            {
                return _filteredContacts;
            }
            private set
            {
                _filteredContacts = value;
                OnPropertyChanged();
            }
        }

        public void SyncDownContacts()
        {
            RegisterSoup();
            string soqlQuery =
                SOQLBuilder.GetInstanceWithFields(ContactObject.ContactFields)
                    .From(Constants.Contact)
                    .Limit(Limit)
                    .Build();
            SyncTarget target = SyncTarget.TargetForSOQLSyncDown(soqlQuery);
            try
            {
                _syncManager.SyncDown(target, ContactSoup, HandleSyncUpdate);
            }
            catch (SmartStoreException)
            {
                SyncDownContacts();
            }
        }

        public void RegisterSoup()
        {
            _store.RegisterSoup(ContactSoup, ContactsIndexSpec);
        }

        public void SyncUpContacts()
        {
            SyncOptions options = SyncOptions.OptionsForSyncUp(ContactObject.ContactFields.ToList());
            _syncManager.SyncUp(options, ContactSoup, HandleSyncUpdate);
        }

        public void DeleteObject(ContactObject contact)
        {
            if (contact == null) return;
            try
            {
                var item = _store.Retrieve(ContactSoup,
                    _store.LookupSoupEntryId(ContactSoup, Constants.Id, contact.ObjectId))[0].ToObject<JObject>();
                item[SyncManager.Local] = true;
                item[SyncManager.LocallyDeleted] = true;
                _store.Upsert(ContactSoup, item);
                contact.Deleted = true;
                UpdateContact(contact);
            }
            catch (Exception)
            {
                Debug.WriteLine("Exception occurred while trying to delete");
            }
        }

        public void SaveContact(ContactObject contact, bool isCreated)
        {
            if (contact == null) return;
            try
            {
                var querySpec = QuerySpec.BuildExactQuerySpec(ContactSoup, Constants.Id, contact.ObjectId, 1);
                var returned = _store.Query(querySpec, 0);
                var item = returned.Count > 0 ? returned[0].ToObject<JObject>() : new JObject();
                item[ContactObject.FirstNameField] = contact.FirstName;
                item[ContactObject.LastNameField] = contact.LastName;
                item[Constants.NameField] = contact.FirstName + contact.LastName;
                item[ContactObject.TitleField] = contact.Title;
                item[ContactObject.DepartmentField] = contact.Department;
                item[ContactObject.PhoneField] = contact.Phone;
                item[ContactObject.EmailField] = contact.Email;
                item[ContactObject.AddressField] = contact.Address;
                item[SyncManager.Local] = true;
                item[SyncManager.LocallyUpdated] = !isCreated;
                item[SyncManager.LocallyCreated] = isCreated;
                item[SyncManager.LocallyDeleted] = false;
                if (isCreated)
                {
                    item[Constants.Id] = "local_" + SmartStore.CurrentTimeMillis;
                    contact.ObjectId = item[Constants.Id].Value<string>();
                    var attributes = new JObject();
                    attributes[Constants.Type.ToLower()] = Constants.Contact;
                    item[Constants.Attributes.ToLower()] = attributes;
                    _store.Create(ContactSoup, item);
                }
                else
                {
                    _store.Upsert(ContactSoup, item);
                }
                contact.UpdatedOrCreated = true;
                UpdateContact(contact);
            }
            catch (Exception)
            {
                Debug.WriteLine("Exception occurred while trying to save");
            }
        }

        private async void UpdateContact(ContactObject obj)
        {
            if (obj == null) return;
            var core = CoreApplication.MainView.CoreWindow.Dispatcher;
            await Task.Delay(10).ContinueWith(async a =>
            {
                await RemoveContact(obj);
            });
            await Task.Delay(10).ContinueWith(a =>
            {
                core.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Contacts.Add(obj);
                    OnPropertyChanged("Contacts");
                });
            });
        }

        private async Task<bool> RemoveContact(ContactObject obj)
        {
            bool result = false;
            var core = CoreApplication.MainView.CoreWindow.Dispatcher;
            await core.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                result = Contacts.Remove(obj);
                OnPropertyChanged("Contacts");
            });
            return result;
        }

        public void ClearSmartStore()
        {
            _store.DropAllSoups();
            _store.ResetDatabase();
        }

        private async void HandleSyncUpdate(SyncState sync)
        {
            if (SyncState.SyncStatusTypes.Done != sync.Status) return;
            switch (sync.SyncType)
            {
                case SyncState.SyncTypes.SyncUp:
                    RemoveDeleted();
                    ResetUpdated();
                    SyncDownContacts();
                    break;
                case SyncState.SyncTypes.SyncDown:
                    LoadDataFromSmartStore();
                    break;
            }
        }

        private async void RemoveDeleted()
        {
            var core = CoreApplication.MainView.CoreWindow.Dispatcher;
            var todelete = Contacts.Select(n => n).Where(n => n.Deleted).ToList();
            for (int index = 0; index < todelete.Count; index++)
            {
                var delete = todelete[index];
                await Task.Delay(10).ContinueWith(async a => { await RemoveContact(delete); });
            }
        }

        private async void ResetUpdated()
        {
            var core = CoreApplication.MainView.CoreWindow.Dispatcher;
            var updated = Contacts.Select(n => n).Where(n => n.UpdatedOrCreated).ToList();
            for (int index = 0; index < updated.Count; index++)
            {
                var update = updated[index];
                update.UpdatedOrCreated = false;
                update.Deleted = false;
                UpdateContact(update);
            }
        }

        private void LoadDataFromSmartStore()
        {
            if (!_store.HasSoup(ContactSoup))
                return;
            QuerySpec querySpec = QuerySpec.BuildAllQuerySpec(ContactSoup, ContactObject.LastNameField,
                QuerySpec.SqlOrder.ASC,
                Limit);

            JArray results = _store.Query(querySpec, 0);
            if (results == null) return;
            var contacts = (from contact in results
                            let model = new ContactObject(contact.Value<JObject>())
                            select model).ToArray();
            for (int i = 0, max = contacts.Length; i < max; i++)
            {
                var t = contacts[i];
                UpdateContact(t);
            }
            RunFilter();
        }

        private async void RunFilter()
        {
            var contacts = Contacts.ToList();
            CoreDispatcher core = CoreApplication.MainView.CoreWindow.Dispatcher;

            await core.RunAsync(CoreDispatcherPriority.Normal, () => FilteredContacts.Clear());
            if (String.IsNullOrWhiteSpace(_filter))
            {

                await core.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (var contact in contacts)
                    {
                        FilteredContacts.Add(contact);
                    }
                });

                return;
            }
            var filtered =
                contacts.Where(
                    contact => !String.IsNullOrEmpty(contact.ContactName) && contact.ContactName.Contains(_filter))
                    .ToList();
            await core.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (var contact in filtered)
                {
                    FilteredContacts.Add(contact);
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}