using DynamicData.Binding;
using Noggog.WPF;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Noggog.UI;

namespace HunterbornExtenderUI
{
    public class VM_Alphabetizer<TSource, TKey> : ViewModel where TSource : notnull
    {
        public ObservableCollection<TSource> SubscribedCollection { get; set; } = new();
        public Func<TSource, TKey> KeySelector { get; set; }
        public string DisplayText { get; set; } = "AZ";
        public ICommand AlphabetizeCommand { get; set; }
        public ICommand UndoCommand { get; set; }
        private SortState State { get; set; } = SortState.Unsorted;
        public SolidColorBrush ButtonColor { get; set; } = new(Colors.White);
        private ObservableCollection<TSource> _originalOrder { get; set; } = new();
        public bool WasSorted { get; set; } = false;

        public VM_Alphabetizer(ObservableCollection<TSource> subscribedCollection, Func<TSource, TKey> keySelector, SolidColorBrush buttonColor)
        {
            SubscribedCollection = subscribedCollection;
            KeySelector = keySelector;
            ButtonColor = buttonColor;

            AlphabetizeCommand = ReactiveCommand.Create(() =>
            {
                Sort();
            });

            
            UndoCommand = ReactiveCommand.Create(() =>
            {
                Revert();
            });

            SubscribedCollection.ToObservableChangeSet().Throttle(TimeSpan.FromMilliseconds(100), RxSchedulers.MainThreadScheduler).Subscribe(x => GetSortState());

            this.WhenAnyValue(x => x.State).Subscribe(x =>
            {
                if (State == SortState.Unsorted || State == SortState.Reversed) { DisplayText = "AZ"; }
                else { DisplayText = "ZA"; }
            });
        }

        private enum SortState
        {
            Forward,
            Reversed,
            Unsorted
        }

        private void Sort()
        {
            if (SubscribedCollection is null) { return; }
            if (!WasSorted)
            {
                _originalOrder = new ObservableCollection<TSource>(SubscribedCollection);
            }

            if (State == SortState.Unsorted || State == SortState.Reversed)
            {
                SubscribedCollection.Sort(KeySelector, false);
            }
            else
            {
                SubscribedCollection.Sort(KeySelector, true);
            }
            WasSorted = true;
        }

        private void Revert()
        {
            if (SubscribedCollection is null) { return; }
            if (WasSorted)
            {
                SubscribedCollection.Clear();
                foreach (var item in _originalOrder)
                {
                    SubscribedCollection.Add(item);
                }
                WasSorted = false;
            }
        }

        private void GetSortState()
        {
            if (SubscribedCollection is null) { return; }
            if (SubscribedCollection.IsSorted(KeySelector, false))
            {
                State = SortState.Forward;
            }
            else if (SubscribedCollection.IsSorted(KeySelector, true))
            {
                State = SortState.Reversed;
            }
            else
            {
                State = SortState.Unsorted;
            }
        }
    }
}
