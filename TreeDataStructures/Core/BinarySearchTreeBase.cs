﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null)
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default;
    public int Count { get; protected set; }
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();

    // ----- Main operations -----
    public virtual void Add(TKey key, TValue value)
    {
        if (FindNode(key) != null)
            throw new ArgumentException("An element with the same key already exists.");

        var newNode = CreateNode(key, value);

        if (Root == null)
        {
            Root = newNode;
            Count++;
            OnNodeAdded(newNode);
            return;
        }

        var current = Root;
        while (true)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp < 0)
            {
                if (current.Left == null)
                {
                    current.Left = newNode;
                    newNode.Parent = current;
                    break;
                }
                current = current.Left;
            }
            else
            {
                if (current.Right == null)
                {
                    current.Right = newNode;
                    newNode.Parent = current;
                    break;
                }
                current = current.Right;
            }
        }

        Count++;
        OnNodeAdded(newNode);
    }

    public virtual bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node == null) return false;

        RemoveNode(node);
        return true;
    }

    protected virtual void RemoveNode(TNode node)
    {
        TNode? balanceStart = null;

        if (node.Left == null)
        {
            balanceStart = node.Parent;
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            balanceStart = node.Parent;
            Transplant(node, node.Left);
        }
        else
        {
            var successor = node.Right;
            while (successor.Left != null)
                successor = successor.Left;

            var oldParentOfSuccessor = successor.Parent;

            if (oldParentOfSuccessor != node)
            {
                Transplant(successor, successor.Right);
                successor.Right = node.Right;
                successor.Right.Parent = successor;
            }

            Transplant(node, successor);
            successor.Left = node.Left;
            successor.Left.Parent = successor;

            balanceStart = node.Parent ?? successor;
        }

        Count--;
        OnNodeRemoved(balanceStart, null);
    }

    protected TNode? FindNode(TKey key)
    {
        var current = Root;
        while (current != null)
        {
            int cmp = Comparer.Compare(key, current.Key);
            if (cmp == 0) return current;
            current = cmp < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected abstract TNode CreateNode(TKey key, TValue value);

    // ----- Hooks -----
    protected virtual void OnNodeAdded(TNode newNode) { }
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }

    // ----- Helpers -----
    protected void Transplant(TNode u, TNode? v)
    {
        if (u.Parent == null)
            Root = v;
        else if (u.IsLeftChild)
            u.Parent.Left = v;
        else
            u.Parent.Right = v;

        if (v != null)
            v.Parent = u.Parent;
    }

    protected virtual void RotateLeft(TNode x)
    {
        var y = x.Right;
        if (y == null) throw new InvalidOperationException("Left rotation requires a right child.");

        x.Right = y.Left;
        if (y.Left != null) y.Left.Parent = x;

        y.Parent = x.Parent;
        if (x.Parent == null)
            Root = y;
        else if (x == x.Parent.Left)
            x.Parent.Left = y;
        else
            x.Parent.Right = y;

        y.Left = x;
        x.Parent = y;
    }

    protected virtual void RotateRight(TNode y)
    {
        var x = y.Left;
        if (x == null) throw new InvalidOperationException("Right rotation requires a left child.");

        y.Left = x.Right;
        if (x.Right != null) x.Right.Parent = y;

        x.Parent = y.Parent;
        if (y.Parent == null)
            Root = x;
        else if (y == y.Parent.Left)
            y.Parent.Left = x;
        else
            y.Parent.Right = x;

        x.Right = y;
        y.Parent = x;
    }

    protected void RotateBigRight(TNode x)
    {
        RotateRight(x.Right!);
        RotateLeft(x);
    }

    protected void RotateBigLeft(TNode y)
    {
        RotateLeft(y.Left!);
        RotateRight(y);
    }

    protected void RotateDoubleLeft(TNode x) => RotateBigLeft(x);
    protected void RotateDoubleRight(TNode y) => RotateBigRight(y);

    // ----- Traversal iterators -----
    private enum TraversalStrategy
    {
        InOrder, PreOrder, PostOrder,
        InOrderReverse, PreOrderReverse, PostOrderReverse
    }

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeIterator(this, TraversalStrategy.InOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => new TreeIterator(this, TraversalStrategy.PreOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => new TreeIterator(this, TraversalStrategy.PostOrder);

    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => new TreeIterator(this, TraversalStrategy.InOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => new TreeIterator(this, TraversalStrategy.PreOrderReverse);

    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeIterator(this, TraversalStrategy.PostOrderReverse);

    private struct TreeIterator : IEnumerable<TreeEntry<TKey, TValue>>, IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly BinarySearchTreeBase<TKey, TValue, TNode> _tree;
        private readonly TraversalStrategy _strategy;
        private TNode? _nextNode;
        private TNode? _current;

        public TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        {
            _tree = tree;
            _strategy = strategy;
            _nextNode = GetFirstNode();
            _current = default;
        }

        private TNode? GetFirstNode()
        {
            if (_tree.Root == null) return null;

            return _strategy switch
            {
                TraversalStrategy.InOrder => GetFirstInOrder(_tree.Root),
                TraversalStrategy.PreOrder => _tree.Root,
                TraversalStrategy.PostOrder => GetFirstPostOrder(_tree.Root),
                TraversalStrategy.InOrderReverse => GetFirstInOrderReverse(_tree.Root),
                TraversalStrategy.PreOrderReverse => GetLastPreOrder(_tree.Root),
                TraversalStrategy.PostOrderReverse => _tree.Root,
                _ => null
            };
        }

        private static TNode GetFirstInOrder(TNode root)
        {
            var node = root;
            while (node.Left != null) node = node.Left;
            return node;
        }

        private static TNode GetFirstPostOrder(TNode root)
        {
            var node = root;
            while (true)
            {
                if (node.Left != null)
                    node = node.Left;
                else if (node.Right != null)
                    node = node.Right;
                else
                    return node;
            }
        }

        private static TNode GetFirstInOrderReverse(TNode root)
        {
            var node = root;
            while (node.Right != null) node = node.Right;
            return node;
        }

        private static TNode GetLastPreOrder(TNode root)
        {
            var node = root;
            while (node.Right != null) node = node.Right;
            return node;
        }

        private TNode? NextInOrder(TNode node)
        {
            if (node.Right != null)
            {
                var cur = node.Right;
                while (cur.Left != null) cur = cur.Left;
                return cur;
            }

            var current = node;
            var parent = current.Parent;
            while (parent != null && current == parent.Right)
            {
                current = parent;
                parent = parent.Parent;
            }
            return parent;
        }

        private TNode? NextPreOrder(TNode node)
        {
            if (node.Left != null)
                return node.Left;

            if (node.Right != null)
                return node.Right;

            var current = node;
            var parent = current.Parent;
            while (parent != null)
            {
                if (current == parent.Left && parent.Right != null)
                    return parent.Right;
                current = parent;
                parent = parent.Parent;
            }
            return null;
        }

        private TNode? NextPostOrder(TNode node)
        {
            var parent = node.Parent;
            if (parent == null) return null;

            if (node == parent.Left)
            {
                if (parent.Right != null)
                    return GetFirstPostOrder(parent.Right);
                return parent;
            }
            else
            {
                return parent;
            }
        }

        private TNode? PrevInOrderReverse(TNode node)
        {
            if (node.Left != null)
            {
                var cur = node.Left;
                while (cur.Right != null) cur = cur.Right;
                return cur;
            }

            var current = node;
            var parent = current.Parent;
            while (parent != null && current == parent.Left)
            {
                current = parent;
                parent = parent.Parent;
            }
            return parent;
        }

        private TNode? PrevPreOrderReverse(TNode node)
        {
            if (node.Parent == null) return null;

            if (node.IsRightChild)
            {
                var parent = node.Parent;
                if (parent.Left != null)
                {
                    var cur = parent.Left;
                    while (cur.Right != null) cur = cur.Right;
                    return cur;
                }
                return parent;
            }
            else
            {
                return node.Parent;
            }
        }

        private TNode? PrevPostOrderReverse(TNode node)
        {
            if (node.Right != null)
            {
                return node.Right;
            }

            if (node.Left != null)
            {
                return node.Left;
            }

            var current = node;
            var parent = current.Parent;
            while (parent != null && current == parent.Left)
            {
                current = parent;
                parent = parent.Parent;
            }

            if (parent == null) return null;

            if (parent.Left != null)
            {
                var cur = parent.Left;
                while (cur.Right != null) cur = cur.Right;
                return cur;
            }
            return parent;
        }

        public TreeEntry<TKey, TValue> Current
        {
            get
            {
                if (_current == null)
                    throw new InvalidOperationException("Enumeration not started or ended");
                return new TreeEntry<TKey, TValue>(_current.Key, _current.Value, ComputeDepth(_current));
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_nextNode == null)
            {
                _current = null;
                return false;
            }

            _current = _nextNode;
            _nextNode = _strategy switch
            {
                TraversalStrategy.InOrder => NextInOrder(_current),
                TraversalStrategy.PreOrder => NextPreOrder(_current),
                TraversalStrategy.PostOrder => NextPostOrder(_current),
                TraversalStrategy.InOrderReverse => PrevInOrderReverse(_current),
                TraversalStrategy.PreOrderReverse => PrevPreOrderReverse(_current),
                TraversalStrategy.PostOrderReverse => PrevPostOrderReverse(_current),
                _ => null
            };
            return true;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() { }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => this;

        private int ComputeDepth(TNode node)
        {
            int depth = 0;
            var cur = node;
            while (cur.Parent != null)
            {
                depth++;
                cur = cur.Parent;
            }
            return depth;
        }
    }

    // ----- IDictionary implementation -----
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return new KeyValueIterator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class KeyValueIterator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TreeIterator _iterator;
        private bool _started;

        public KeyValueIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree)
        {
            _iterator = new TreeIterator(tree, TraversalStrategy.InOrder);
            _started = false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            get
            {
                var entry = _iterator.Current;
                return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                return _iterator.MoveNext();
            }
            return _iterator.MoveNext();
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;

    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var val) ? val : throw new KeyNotFoundException();
        set
        {
            var node = FindNode(key);
            if (node == null)
                Add(key, value);
            else
                node.Value = value;
        }
    }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() { Root = null; Count = 0; }
    public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex >= array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("Insufficient space in the array.");

        foreach (var entry in InOrder())
        {
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);
}