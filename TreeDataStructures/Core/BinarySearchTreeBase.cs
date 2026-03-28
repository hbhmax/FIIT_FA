﻿using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode> : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;
    public IComparer<TKey> Comparer { get; protected set; }
    public int Count { get; protected set; }
    public bool IsReadOnly => false;

    // Конструктор
    protected BinarySearchTreeBase(IComparer<TKey>? comparer = null)
    {
        Comparer = comparer ?? Comparer<TKey>.Default;
    }

    // IDictionary implementation
    public ICollection<TKey> Keys => InOrder().Select(e => e.Key).ToList();
    public ICollection<TValue> Values => InOrder().Select(e => e.Value).ToList();

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

    // --- Хуки для наследников ---
    protected virtual void OnNodeAdded(TNode newNode) { }
    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) { }

    // --- Фабричный метод ---
    protected abstract TNode CreateNode(TKey key, TValue value);

    // --- Поиск узла по ключу ---
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

    // --- Трансплантация поддерева ---
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

    // --- Повороты ---
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

    // --- Итераторы обхода (без yield) ---
    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeIterator(this, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => new TreeIterator(this, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => new TreeIterator(this, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => new TreeIterator(this, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => new TreeIterator(this, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeIterator(this, TraversalStrategy.PostOrderReverse);

    // --- Внутренний итератор (структура) ---
    private struct TreeIterator : IEnumerable<TreeEntry<TKey, TValue>>, IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly BinarySearchTreeBase<TKey, TValue, TNode> _tree;
        private readonly TraversalStrategy _strategy;
        private Stack<(TNode node, int depth, byte state)>? _stack;
        private TreeEntry<TKey, TValue> _current;

        public TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        {
            _tree = tree;
            _strategy = strategy;
            _stack = null;
            _current = default;
        }

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator()
        {
            _stack = new Stack<(TNode, int, byte)>();
            _current = default;

            if (_tree.Root != null)
                InitializeStack();

            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TreeEntry<TKey, TValue> Current => _current;
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_stack == null || _stack.Count == 0)
                return false;

            while (_stack.Count > 0)
            {
                var (node, depth, state) = _stack.Pop();

                switch (_strategy)
                {
                    case TraversalStrategy.InOrder:
                        if (state == 0)
                        {
                            _stack.Push((node, depth, 1));
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                        }
                        else if (state == 1)
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            _stack.Push((node, depth, 2));
                            return true;
                        }
                        else
                        {
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                        }
                        break;

                    case TraversalStrategy.PreOrder:
                        if (state == 0)
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            _stack.Push((node, depth, 1));
                            return true;
                        }
                        else if (state == 1)
                        {
                            _stack.Push((node, depth, 2));
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                        }
                        else
                        {
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                        }
                        break;

                    case TraversalStrategy.PostOrder:
                        if (state == 0)
                        {
                            _stack.Push((node, depth, 1));
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                        }
                        else if (state == 1)
                        {
                            _stack.Push((node, depth, 2));
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                        }
                        else
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            return true;
                        }
                        break;

                    case TraversalStrategy.InOrderReverse:
                        if (state == 0)
                        {
                            _stack.Push((node, depth, 1));
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                        }
                        else if (state == 1)
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            _stack.Push((node, depth, 2));
                            return true;
                        }
                        else
                        {
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                        }
                        break;

                    case TraversalStrategy.PreOrderReverse:
                        if (state == 0)
                        {
                            _stack.Push((node, depth, 1));
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                        }
                        else if (state == 1)
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            return true;
                        }
                        break;

                    case TraversalStrategy.PostOrderReverse:
                        if (state == 0)
                        {
                            _current = new TreeEntry<TKey, TValue>(node.Key, node.Value, depth);
                            if (node.Left != null) _stack.Push((node.Left, depth + 1, 0));
                            if (node.Right != null) _stack.Push((node.Right, depth + 1, 0));
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }

        private void InitializeStack()
        {
            _stack!.Push((_tree.Root!, 0, 0));
        }
    }

    private enum TraversalStrategy
    {
        InOrder, PreOrder, PostOrder,
        InOrderReverse, PreOrderReverse, PostOrderReverse
    }

    // --- Реализация IDictionary (остальные члены) ---
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var entry in InOrder())
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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