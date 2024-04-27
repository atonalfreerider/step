using System;
using System.Collections;
using System.Collections.Generic;

namespace IxMilia.Step.Collections
{
    public class ListWithMinimumAndMaximum<T>(int minimum, int maximum)
        : ListWithPredicates<T>(null, list => list.Count >= minimum && list.Count <= maximum, false);

    public class ListWithPredicates<T> : IList<T>
    {
        readonly List<T> _items = [];
        public Func<T, bool> ItemPredicate { get; }
        public Func<ListWithPredicates<T>, bool> CollectionPredicate { get; }

        public ListWithPredicates(Func<T, bool> itemPredicate, Func<ListWithPredicates<T>, bool> collectionPredicate, params T[] initialItems)
            : this(itemPredicate, collectionPredicate, true, initialItems)
        {
        }

        public ListWithPredicates(Func<T, bool> itemPredicate, Func<ListWithPredicates<T>, bool> collectionPredicate, bool validateInitialCount, params T[] initialItems)
        {
            ItemPredicate = itemPredicate;
            CollectionPredicate = collectionPredicate;
            foreach (T item in initialItems)
            {
                Add(item);
            }

            if (validateInitialCount)
            {
                ValidateCollectionPredicate();
            }
        }

        internal void AssignValues(IEnumerable<T> values)
        {
            _items.Clear();
            foreach (T value in values)
            {
                Add(value);
            }

            ValidateCollectionPredicate();
        }

        void ValidateItemPredicate(T item)
        {
            if (ItemPredicate != null && !ItemPredicate(item))
            {
                throw new InvalidOperationException("Item does not meet the criteria to be added to this collection.");
            }
        }

        void ValidateCollectionPredicate()
        {
            if (CollectionPredicate != null && !CollectionPredicate(this))
            {
                throw new InvalidOperationException("Collection does not meet the criteria to be added to this collection.");
            }
        }

        public T this[int index]
        {
            get => _items[index];
            set
            {
                ValidateItemPredicate(value);
                _items[index] = value;
            }
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            ValidateItemPredicate(item);
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
            ValidateCollectionPredicate();
        }

        public bool Contains(T item) => _items.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(T item) => _items.IndexOf(item);

        public void Insert(int index, T item)
        {
            ValidateItemPredicate(item);
            _items.Insert(index, item);
        }

        public bool Remove(T item)
        {
            bool result = _items.Remove(item);
            ValidateCollectionPredicate();
            return result;
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            ValidateCollectionPredicate();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    }
}
