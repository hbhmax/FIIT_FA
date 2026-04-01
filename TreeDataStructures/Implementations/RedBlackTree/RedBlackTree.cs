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

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child) { }

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
}