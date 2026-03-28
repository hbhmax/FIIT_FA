using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
    where TKey : IComparable<TKey>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var node = FindNode(key);
        if (node != null)
        {
            Splay(node);
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    public override void Add(TKey key, TValue value)
    {
        base.Add(key, value);
        var node = FindNode(key);
        Splay(node!);
    }

    public override bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node == null) return false;

        Splay(node);
        base.Remove(key);
        return true;
    }

    private void Splay(BstNode<TKey, TValue> x)
    {
        while (x.Parent != null)
        {
            var p = x.Parent;
            var g = p.Parent;

            if (g == null)
            {
                if (x.IsLeftChild)
                    RotateRight(p);
                else
                    RotateLeft(p);
            }
            else if (x.IsLeftChild && p.IsLeftChild)
            {
                RotateRight(g);
                RotateRight(p);
            }
            else if (x.IsRightChild && p.IsRightChild)
            {
                RotateLeft(g);
                RotateLeft(p);
            }
            else if (x.IsLeftChild && p.IsRightChild)
            {
                RotateRight(p);
                RotateLeft(g);
            }
            else
            {
                RotateLeft(p);
                RotateRight(g);
            }
        }
    }

    public override bool ContainsKey(TKey key)
    {
        var node = FindNode(key);
        if (node != null)
        {
            Splay(node);
            return true;
        }
        return false;
    }
}