﻿using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value) { Priority = Random.Shared.Next() };

    public override void Add(TKey key, TValue value)
    {
        if (FindNode(key) != null)
            throw new ArgumentException("An element with the same key already exists.");

        var newNode = CreateNode(key, value);
        var (left, right) = Split(Root, key);
        Root = Merge(Merge(left, newNode), right);
        Count++;
    }

    public override bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node == null) return false;

        var merged = Merge(node.Left, node.Right);

        if (node.Parent == null)
            Root = merged;
        else if (node.IsLeftChild)
            node.Parent.Left = merged;
        else
            node.Parent.Right = merged;

        if (merged != null)
            merged.Parent = node.Parent;

        Count--;
        return true;
    }

    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null) return (null, null);

        if (Comparer.Compare(key, root.Key) < 0)
        {
            var (left, right) = Split(root.Left, key);
            root.Left = right;
            if (right != null) right.Parent = root;
            if (left != null) left.Parent = null;
            return (left, root);
        }
        else
        {
            var (left, right) = Split(root.Right, key);
            root.Right = left;
            if (left != null) left.Parent = root;
            if (right != null) right.Parent = null;
            return (root, right);
        }
    }

    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right);
            if (left.Right != null) left.Right.Parent = left;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left);
            if (right.Left != null) right.Left.Parent = right;
            return right;
        }
    }

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode) { }
    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child) { }
}