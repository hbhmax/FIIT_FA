using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue>: BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    private static RbNode<TKey, TValue>? Grandparent(RbNode<TKey, TValue>? n) => n?.Parent?.Parent;

    private static RbNode<TKey, TValue>? Uncle(RbNode<TKey, TValue> n)
    {
        if (n.Parent == null) return null;
        return n.Parent.IsLeftChild ? Grandparent(n)?.Right : Grandparent(n)?.Left;
    }

    protected override void OnNodeAdded(RbNode<TKey, TValue> node)
    {
        node.Color = RbColor.Red;
        InsertCase1(node);
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

            var gp = Grandparent(node)!;
            gp.Color = RbColor.Red;

            InsertCase1(gp);
        }
        else
            InsertCase4(node);
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
        var gp = parent.Parent!;

        parent.Color = RbColor.Black;
        gp.Color = RbColor.Red;

        if (node.IsLeftChild && parent.IsLeftChild)
            RotateRight(gp);
        else
            RotateLeft(gp);
    }


    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        if (node.Left != null && node.Right != null)
        {
            var successor = node.Right;

            while (successor.Left != null)
                successor = successor.Left;

            (node.Key, successor.Key) = (successor.Key, node.Key);
            (node.Value, successor.Value) = (successor.Value, node.Value);

            node = successor;
        }

        var child = node.Left ?? node.Right;

        if (node.Color == RbColor.Red)
        {
            ReplaceNode(node, child);
            Count--;
            return;
        }

        if (GetColor(child) == RbColor.Red)
        {
            ReplaceNode(node, child);
            child!.Color = RbColor.Black;
            Count--;
            return;
        }

        var parent = node.Parent;

        ReplaceNode(node, child);

        RemoveCase1(child, parent);

        Count--;
    }

    private void RemoveCase1(RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent)
    {
        if (parent != null)
            RemoveCase2(node, parent);
    }

    private void RemoveCase2(RbNode<TKey, TValue>? node, RbNode<TKey, TValue> parent)
    {
        var sibling = Sibling(node, parent);

        if (GetColor(sibling) == RbColor.Red)
        {
            parent.Color = RbColor.Red;
            sibling!.Color = RbColor.Black;

            if (sibling.IsLeftChild)
                RotateRight(parent);
            else
                RotateLeft(parent);
        }

        RemoveCase3(node, parent);
    }

    private void RemoveCase3(RbNode<TKey, TValue>? node, RbNode<TKey, TValue> parent)
    {
        var sibling = Sibling(node, parent);

        if (GetColor(parent) == RbColor.Black &&
            GetColor(sibling) == RbColor.Black &&
            GetColor(sibling?.Left) == RbColor.Black &&
            GetColor(sibling?.Right) == RbColor.Black)
        {
            if (sibling != null)
                sibling.Color = RbColor.Red;

            RemoveCase1(parent, parent.Parent);
        }
        else
            RemoveCase4(node, parent);
    }

    private void RemoveCase4(RbNode<TKey, TValue>? node, RbNode<TKey, TValue> parent)
    {
        var sibling = Sibling(node, parent);

        if (GetColor(parent) == RbColor.Red &&
            GetColor(sibling) == RbColor.Black &&
            GetColor(sibling?.Left) == RbColor.Black &&
            GetColor(sibling?.Right) == RbColor.Black)
        {
            sibling!.Color = RbColor.Red;
            parent.Color = RbColor.Black;
        }
        else
            RemoveCase5(node, parent);
    }

    private void RemoveCase5(RbNode<TKey, TValue>? node, RbNode<TKey, TValue> parent)
    {
        var sibling = Sibling(node, parent);

        if (GetColor(sibling) == RbColor.Black)
        {
            if (node == parent.Left &&
                GetColor(sibling?.Left) == RbColor.Red &&
                GetColor(sibling?.Right) == RbColor.Black)
            {
                sibling!.Color = RbColor.Red;
                sibling.Left!.Color = RbColor.Black;
                RotateRight(sibling);
            }
            else if (node == parent.Right &&
                     GetColor(sibling?.Right) == RbColor.Red &&
                     GetColor(sibling?.Left) == RbColor.Black)
            {
                sibling!.Color = RbColor.Red;
                sibling.Right!.Color = RbColor.Black;
                RotateLeft(sibling);
            }
        }

        RemoveCase6(node, parent);
    }

    private void RemoveCase6(RbNode<TKey, TValue>? node, RbNode<TKey, TValue> parent)
    {
        var sibling = Sibling(node, parent);
        if (sibling == null) return;

        sibling.Color = parent.Color;
        parent.Color = RbColor.Black;

        if (node == parent.Left)
        {
            if (sibling.Right != null)
                sibling.Right.Color = RbColor.Black;

            RotateLeft(parent);
        }
        else
        {
            if (sibling.Left != null)
                sibling.Left.Color = RbColor.Black;

            RotateRight(parent);
        }
    }

    private void ReplaceNode(RbNode<TKey, TValue> node, RbNode<TKey, TValue>? child)
    {
        if (node.Parent == null)
            Root = child;
        else if (node.IsLeftChild)
            node.Parent.Left = child;
        else
            node.Parent.Right = child;

        if (child != null)
            child.Parent = node.Parent;
    }

    private static RbNode<TKey, TValue>? Sibling( RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent)
    {
        if (parent == null) return null;
        return parent.Left == node ? parent.Right : parent.Left;
    }

    private static RbColor GetColor(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child) {}
}