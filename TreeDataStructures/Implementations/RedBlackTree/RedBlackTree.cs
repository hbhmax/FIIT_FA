using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    private RbColor? _deletedNodeColor;

    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    private static RbNode<TKey, TValue>? Grandparent(RbNode<TKey, TValue>? node)
        => node?.Parent?.Parent;

    private static RbNode<TKey, TValue>? Uncle(RbNode<TKey, TValue> node)
    {
        if (node.Parent == null) return null;
        return node.Parent.IsLeftChild ? Grandparent(node)?.Right : Grandparent(node)?.Left;
    }

    private static RbNode<TKey, TValue>? Sibling(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
        => parent.Left == child ? parent.Right : parent.Left;

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        newNode.Color = RbColor.Red;
        InsertCase1(newNode);
    }

    private void InsertCase1(RbNode<TKey, TValue> node)
    {
        if (node.Parent == null)
            node.Color = RbColor.Black;
        else
            InsertCase2(node);
    }

    private void InsertCase2(RbNode<TKey, TValue> node)
    {
        if (node.Parent!.Color == RbColor.Red)
            InsertCase3(node);
    }

    private void InsertCase3(RbNode<TKey, TValue> node)
    {
        var uncle = Uncle(node);
        if (uncle != null && uncle.Color == RbColor.Red)
        {
            node.Parent!.Color = RbColor.Black;
            uncle.Color = RbColor.Black;
            var grandparent = Grandparent(node);
            grandparent!.Color = RbColor.Red;
            InsertCase1(grandparent);
        }
        else
        {
            InsertCase4(node);
        }
    }

    private void InsertCase4(RbNode<TKey, TValue> node)
    {
        var parent = node.Parent!;
        if (node.IsRightChild && parent.IsLeftChild)
        {
            RotateLeft(parent);
            node = parent;
        }
        else if (node.IsLeftChild && parent.IsRightChild)
        {
            RotateRight(parent);
            node = parent;
        }
        InsertCase5(node);
    }

    private void InsertCase5(RbNode<TKey, TValue> node)
    {
        var parent = node.Parent!;
        var grandparent = parent.Parent!;
        parent.Color = RbColor.Black;
        grandparent.Color = RbColor.Red;
        if (node.IsLeftChild && parent.IsLeftChild)
            RotateRight(grandparent);
        else
            RotateLeft(grandparent);
    }

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        _deletedNodeColor = node.Color;
        base.RemoveNode(node);
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        if (_deletedNodeColor == RbColor.Red)
        {
            _deletedNodeColor = null;
            return;
        }

        _deletedNodeColor = null;

        if (GetColor(child) == RbColor.Red)
        {
            SetColor(child, RbColor.Black);
            return;
        }

        RemoveCase1(parent, child);
    }

    private void RemoveCase1(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        if (parent != null)
            RemoveCase2(parent, child);
    }

    private void RemoveCase2(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
    {
        var sibling = Sibling(parent, child);
        if (sibling != null && sibling.Color == RbColor.Red)
        {
            parent.Color = RbColor.Red;
            sibling.Color = RbColor.Black;
            if (sibling.IsLeftChild)
                RotateRight(parent);
            else
                RotateLeft(parent);
        }
        RemoveCase3(parent, child);
    }

    private void RemoveCase3(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
    {
        var sibling = Sibling(parent, child);
        if (parent.Color == RbColor.Black &&
            sibling != null && sibling.Color == RbColor.Black &&
            GetColor(sibling.Left) == RbColor.Black &&
            GetColor(sibling.Right) == RbColor.Black)
        {
            sibling.Color = RbColor.Red;
            RemoveCase1(parent.Parent, parent);
        }
        else
        {
            RemoveCase4(parent, child);
        }
    }

    private void RemoveCase4(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
    {
        var sibling = Sibling(parent, child);
        if (parent.Color == RbColor.Red &&
            sibling != null && sibling.Color == RbColor.Black &&
            GetColor(sibling.Left) == RbColor.Black &&
            GetColor(sibling.Right) == RbColor.Black)
        {
            sibling.Color = RbColor.Red;
            parent.Color = RbColor.Black;
        }
        else
        {
            RemoveCase5(parent, child);
        }
    }

    private void RemoveCase5(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
    {
        var sibling = Sibling(parent, child);
        if (sibling != null && sibling.Color == RbColor.Black)
        {
            if (sibling.IsLeftChild &&
                GetColor(sibling.Left) == RbColor.Red &&
                GetColor(sibling.Right) == RbColor.Black)
            {
                sibling.Color = RbColor.Red;
                sibling.Left!.Color = RbColor.Black;
                RotateRight(sibling);
            }
            else if (sibling.IsRightChild &&
                     GetColor(sibling.Right) == RbColor.Red &&
                     GetColor(sibling.Left) == RbColor.Black)
            {
                sibling.Color = RbColor.Red;
                sibling.Right!.Color = RbColor.Black;
                RotateLeft(sibling);
            }
        }
        RemoveCase6(parent, child);
    }

    private void RemoveCase6(RbNode<TKey, TValue> parent, RbNode<TKey, TValue>? child)
    {
        var sibling = Sibling(parent, child);
        if (sibling == null) return;

        sibling.Color = parent.Color;
        parent.Color = RbColor.Black;
        if (sibling.IsRightChild)
        {
            if (sibling.Right != null) sibling.Right.Color = RbColor.Black;
            RotateLeft(parent);
        }
        else
        {
            if (sibling.Left != null) sibling.Left.Color = RbColor.Black;
            RotateRight(parent);
        }
    }

    private static RbColor GetColor(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;
    private static void SetColor(RbNode<TKey, TValue>? node, RbColor color) { if (node != null) node.Color = color; }
}