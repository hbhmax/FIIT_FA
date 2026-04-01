using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new RbNode<TKey, TValue>(key, value);

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        newNode.Color = RbColor.Red;
        FixAfterInsert(newNode);
    }

    private void FixAfterInsert(RbNode<TKey, TValue> node)
    {
        while (node != Root && node.Parent!.Color == RbColor.Red)
        {
            if (node.Parent == node.Parent.Parent?.Left)
            {
                var uncle = node.Parent.Parent.Right;
                if (uncle != null && uncle.Color == RbColor.Red)
                {
                    node.Parent.Color = RbColor.Black;
                    uncle.Color = RbColor.Black;
                    node.Parent.Parent.Color = RbColor.Red;
                    node = node.Parent.Parent;
                }
                else
                {
                    if (node == node.Parent.Right)
                    {
                        node = node.Parent;
                        RotateLeft(node);
                    }
                    node.Parent!.Color = RbColor.Black;
                    node.Parent.Parent!.Color = RbColor.Red;
                    RotateRight(node.Parent.Parent);
                }
            }
            else
            {
                var uncle = node.Parent.Parent?.Left;
                if (uncle != null && uncle.Color == RbColor.Red)
                {
                    node.Parent.Color = RbColor.Black;
                    uncle.Color = RbColor.Black;
                    node.Parent.Parent!.Color = RbColor.Red;
                    node = node.Parent.Parent;
                }
                else
                {
                    if (node == node.Parent.Left)
                    {
                        node = node.Parent;
                        RotateRight(node);
                    }
                    node.Parent!.Color = RbColor.Black;
                    node.Parent.Parent!.Color = RbColor.Red;
                    RotateLeft(node.Parent.Parent);
                }
            }
        }
        Root!.Color = RbColor.Black;
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
        if (GetColor(child) == RbColor.Red)
        {
            SetColor(child, RbColor.Black);
            return;
        }
        FixAfterDelete(child, parent);
    }

    private void FixAfterDelete(RbNode<TKey, TValue>? x, RbNode<TKey, TValue>? parent)
    {
        while (x != Root && GetColor(x) == RbColor.Black)
        {
            if (x == parent?.Left)
            {
                var w = parent.Right;
                if (GetColor(w) == RbColor.Red)
                {
                    SetColor(w, RbColor.Black);
                    SetColor(parent, RbColor.Red);
                    RotateLeft(parent);
                    w = parent.Right;
                }

                if (GetColor(w?.Left) == RbColor.Black && GetColor(w?.Right) == RbColor.Black)
                {
                    SetColor(w, RbColor.Red);
                    x = parent;
                    parent = x?.Parent;
                }
                else
                {
                    if (GetColor(w?.Right) == RbColor.Black)
                    {
                        SetColor(w?.Left, RbColor.Black);
                        SetColor(w, RbColor.Red);
                        RotateRight(w);
                        w = parent.Right;
                    }
                    SetColor(w, GetColor(parent));
                    SetColor(parent, RbColor.Black);
                    SetColor(w?.Right, RbColor.Black);
                    RotateLeft(parent);
                    x = Root;
                }
            }
            else
            {
                var w = parent.Left;
                if (GetColor(w) == RbColor.Red)
                {
                    SetColor(w, RbColor.Black);
                    SetColor(parent, RbColor.Red);
                    RotateRight(parent);
                    w = parent.Left;
                }
                if (GetColor(w?.Right) == RbColor.Black && GetColor(w?.Left) == RbColor.Black)
                {
                    SetColor(w, RbColor.Red);
                    x = parent;
                    parent = x?.Parent;
                }
                else
                {
                    if (GetColor(w?.Left) == RbColor.Black)
                    {
                        SetColor(w?.Right, RbColor.Black);
                        SetColor(w, RbColor.Red);
                        RotateLeft(w);
                        w = parent.Left;
                    }
                    SetColor(w, GetColor(parent));
                    SetColor(parent, RbColor.Black);
                    SetColor(w?.Left, RbColor.Black);
                    RotateRight(parent);
                    x = Root;
                }
            }
        }
        SetColor(x, RbColor.Black);
    }

    private static RbColor GetColor(RbNode<TKey, TValue>? node) => node?.Color ?? RbColor.Black;

    private static void SetColor(RbNode<TKey, TValue>? node, RbColor color)
    {
        if (node != null) node.Color = color;
    }
}