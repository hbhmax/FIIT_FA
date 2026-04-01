﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null) : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default;
    public int Count { get; protected set; }
    public bool IsReadOnly => false;

    // IDictionary implementation
    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();

    // Main functions
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

    // Hooks (вспомогательные функции после определенного действия)
    protected virtual void OnNodeAdded(TNode newNode) { }
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }

    // Transplant mini-trees
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

    // Rotates 
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
    protected void RotateBigLeft(TNode x)
    {
        RotateLeft(x.Left!);
        RotateRight(x);
    }
    protected void RotateBigRight(TNode y)
    {
        RotateRight(y.Right!);
        RotateLeft(y);
    }
    protected void RotateDoubleLeft(TNode x) => RotateBigLeft(x);
    protected void RotateDoubleRight(TNode y) => RotateBigRight(y);    
    
    // Iterators
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
        private Stack<TNode> _nodeStack;
        private HashSet<TNode> _visited;
        private TreeEntry<TKey, TValue> _current;

        public TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        {
            _tree = tree;
            _strategy = strategy;
            _nodeStack = new Stack<TNode>();
            _visited = new HashSet<TNode>();
            _current = default;
            Initialize();
        }

        private void Initialize()
        {
            _nodeStack.Clear();
            _visited.Clear();

            if (_tree.Root == null) return;

            switch (_strategy)
            {
                case TraversalStrategy.InOrder:
                    PushLeftPath(_tree.Root);
                    break;
                case TraversalStrategy.InOrderReverse:
                    PushRightPath(_tree.Root);
                    break;
                case TraversalStrategy.PreOrder:
                    _nodeStack.Push(_tree.Root);
                    break;
                case TraversalStrategy.PreOrderReverse:
                    FillReverseStackPreOrder(_tree.Root);
                    break;
                case TraversalStrategy.PostOrder:
                    _nodeStack.Push(_tree.Root);
                    break;
                case TraversalStrategy.PostOrderReverse:
                    FillReverseStackPostOrder(_tree.Root);
                    break;
            }
        }

        private void PushLeftPath(TNode node)
        {
            while (node != null)
            {
                _nodeStack.Push(node);
                node = node.Left!;
            }
        }

        private void PushRightPath(TNode node)
        {
            while (node != null)
            {
                _nodeStack.Push(node);
                node = node.Right!;
            }
        }

        private void FillReverseStackPreOrder(TNode root)
        {
            var stack = new Stack<TNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                _nodeStack.Push(node);
                if (node.Right != null) stack.Push(node.Right);
                if (node.Left != null) stack.Push(node.Left);
            }
        }

        private void FillReverseStackPostOrder(TNode root)
        {
            var stack = new Stack<TNode>();
            stack.Push(root);
            var output = new Stack<TNode>();
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                output.Push(node);
                if (node.Left != null) stack.Push(node.Left);
                if (node.Right != null) stack.Push(node.Right);
            }
            while (output.Count > 0)
            {
                _nodeStack.Push(output.Pop());
            }
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator()
        {
            Initialize();
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TreeEntry<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_nodeStack.Count == 0) return false;

            if (_strategy == TraversalStrategy.PreOrderReverse || _strategy == TraversalStrategy.PostOrderReverse)
            {
                TNode node = _nodeStack.Pop();
                _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, CalculateDepth(node));
                return true;
            }

            TNode currentNode = _nodeStack.Pop();
            int depth = CalculateDepth(currentNode);

            switch (_strategy)
            {
                case TraversalStrategy.InOrder:
                    _current = new TreeEntry<TKey, TValue>(currentNode.Key, currentNode.Value, depth);
                    if (currentNode.Right != null)
                        PushLeftPath(currentNode.Right);
                    return true;

                case TraversalStrategy.InOrderReverse:
                    _current = new TreeEntry<TKey, TValue>(currentNode.Key, currentNode.Value, depth);
                    if (currentNode.Left != null)
                        PushRightPath(currentNode.Left);
                    return true;

                case TraversalStrategy.PreOrder:
                    _current = new TreeEntry<TKey, TValue>(currentNode.Key, currentNode.Value, depth);
                    if (currentNode.Right != null) _nodeStack.Push(currentNode.Right);
                    if (currentNode.Left != null) _nodeStack.Push(currentNode.Left);
                    return true;

                case TraversalStrategy.PostOrder:
                    if (!_visited.Contains(currentNode))
                    {
                        _visited.Add(currentNode);
                        _nodeStack.Push(currentNode);
                        if (currentNode.Right != null) _nodeStack.Push(currentNode.Right);
                        if (currentNode.Left != null) _nodeStack.Push(currentNode.Left);
                        return MoveNext();
                    }
                    else
                    {
                        _current = new TreeEntry<TKey, TValue>(currentNode.Key, currentNode.Value, depth);
                        return true;
                    }

                default:
                    return false;
            }
        }

        private int CalculateDepth(TNode node)
        {
            int depth = 0;
            TNode current = node;
            while (current.Parent != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }

    // In IDictionary
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return new KeyValueIterator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class KeyValueIterator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TreeIterator _treeIterator;
        private bool _started;

        public KeyValueIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree)
        {
            _treeIterator = new TreeIterator(tree, TraversalStrategy.InOrder);
            _started = false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            get
            {
                var entry = _treeIterator.Current;
                return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                return _treeIterator.MoveNext();
            }
            return _treeIterator.MoveNext();
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;
    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)  //Атрибут показывает если return false, то value может быть null (или default)
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

    public TValue this[TKey key]  // Перегрузка индексатора []
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